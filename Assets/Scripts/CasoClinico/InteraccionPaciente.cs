using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traduce el input del jugador (manos, botones diegéticos) en eventos abstractos
/// que el GestorCasoClinico reenvía al MotorEvaluacion.
///
/// No conoce al MotorEvaluacion ni a la UI de resultados. Nunca muestra
/// correcto/incorrecto: solo notifica "el jugador hizo X".
///
/// Restricción de allocations en Update():
///   - Nombres de región cacheados en Inicializar() (Transform.name asigna string).
///   - Proximidad por sqrMagnitude contra el umbral al cuadrado (sin Vector3.Distance).
///   - Cooldown por región (_ultimoAvisoRegion) evita disparar el evento muchas veces
///     por frame mientras la mano permanece dentro del radio, pero SÍ permite
///     re-examinar la misma región tras el cooldown (a diferencia de un dedupe
///     permanente). El "examinada al menos una vez" para la evaluación lo acumula
///     el MotorEvaluacion aguas abajo (HashSet.Add idempotente).
/// </summary>
public class InteraccionPaciente : MonoBehaviour
{
    [Header("Regiones anatómicas del paciente")]
    [Tooltip("Los nombres de estos Transform deben coincidir con HallazgoRegion.regionAnatomica.")]
    [SerializeField] private Transform[] regionesAnatomicas;

    [Header("Manos (seguimiento nativo)")]
    [SerializeField] private Transform manoIzquierda;
    [SerializeField] private Transform manoDerecha;

    [Header("Detección de examen físico")]
    [Tooltip("Distancia mano↔región para contar como 'examinada', en metros. Ajustar en pruebas.")]
    [SerializeField] private float umbralProximidadRegion = 0.15f;

    [Tooltip("Segundos mínimos entre dos avisos consecutivos de la MISMA región (evita " +
             "spam de OnRegionExaminada mientras la mano sigue dentro del radio). No es un " +
             "límite de veces: pasado el cooldown la región se puede re-examinar.")]
    [SerializeField] private float cooldownReexamenSeg = 0.75f;

    // Eventos hacia el GestorCasoClinico (nunca hacia el Motor directamente)
    public event Action<string> OnRegionExaminada;
    public event Action<string> OnPreguntaAlternativaSeleccionada;
    public event Action<string> OnTextoLibreEnviado;        // pregunta de anamnesis (texto libre)
    public event Action<string> OnDiagnosticoTextoEnviado;  // diagnóstico registrado (texto libre)
    public event Action<string> OnExamenSolicitado;

    /// <summary>true entre Inicializar() y el fin del caso. Fuera de eso, Update() no evalúa proximidad.</summary>
    public bool DeteccionActiva { get; private set; }

    // Congela Update() (vía GestorCasoClinico.EstablecerPausa) sin tocar
    // _ultimoAvisoRegion ni ningún otro estado acumulado.
    private bool pausado;
    public void SetPausado(bool valor) => pausado = valor;

    /// <summary>Submodo con el que se inicializó el caso. La UI (sección 2) lo usa
    /// para decidir el nivel de ayuda de los paneles diegéticos.</summary>
    public SubmodoCaso SubmodoActivo => submodoActivo;

    // ── Estado interno ───────────────────────────────────────────────────────

    private SubmodoCaso submodoActivo;

    // Momento (Time.time) del último aviso de OnRegionExaminada por NOMBRE de región.
    // Throttle, no dedupe permanente: pasado cooldownReexamenSeg la región vuelve a avisar.
    private readonly Dictionary<string, float> _ultimoAvisoRegion = new Dictionary<string, float>();

    // Cache para Update() sin allocations
    private string[] _nombresRegion;
    private float _umbralProximidadSqr;

    // Teclado nativo del sistema (Quest 3). Su apertura es asíncrona → se hace
    // polling del estado en Update() hasta que el usuario confirma o cancela.
    private TouchScreenKeyboard _teclado;
    private Action<string> _alConfirmarTeclado;

    // Logging de diagnóstico del teclado (BUG 1, 2026-09-11): se deja a propósito
    // para validar en dispositivo — solo registra transiciones de estado, no por
    // frame. Ver informe del bug: la causa confirmada es de configuración de
    // plataforma (OVRProjectConfig.requiresSystemKeyboard), no de cableado.
    private TouchScreenKeyboard.Status _ultimoStatusLogueado = (TouchScreenKeyboard.Status)(-1);

    // ── Ciclo de vida Unity ──────────────────────────────────────────────────

    void Awake()
    {
        CachearNombresRegion();
        RecalcularUmbralSqr();
    }

    void OnValidate() => RecalcularUmbralSqr();

    void Update()
    {
        ProcesarTeclado();               // acción discreta — se atiende aunque no haya detección
        if (pausado) return;
        if (!DeteccionActiva) return;
        DetectarProximidadManoRegion();
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>Prepara el subsistema para un caso concreto. La UI (sección 2)
    /// usará 'caso' para poblar dinámicamente los botones de alternativas y de
    /// exámenes; aquí solo se resetea el estado de detección.</summary>
    public void Inicializar(FichaCaso caso, SubmodoCaso submodo)
    {
        submodoActivo = submodo;
        _ultimoAvisoRegion.Clear();
        CachearNombresRegion();
        RecalcularUmbralSqr();
        DeteccionActiva = true;
        DescartarTeclado();
        // NOTA (sección 2): poblar botones de alternativas (caso.preguntasClave)
        //                   y de exámenes (caso.examenesDisponibles) aquí.
    }

    /// <summary>Detiene la detección de proximidad (fin del caso).</summary>
    public void DetenerDeteccion()
    {
        DeteccionActiva = false;
        DescartarTeclado();
    }

    // Llamado desde los botones de alternativas generados dinámicamente (pinch).
    public void SeleccionarPreguntaAlternativa(string idPregunta) =>
        OnPreguntaAlternativaSeleccionada?.Invoke(idPregunta);

    /// <summary>Abre el teclado nativo del sistema para una pregunta de anamnesis.
    /// Al confirmar (con texto no vacío) dispara <see cref="OnTextoLibreEnviado"/>;
    /// al cancelar, nada.</summary>
    public void AbrirTecladoAnamnesis()
    {
        Debug.Log("[InteraccionPaciente] AbrirTecladoAnamnesis() llamado.", this);
        AbrirTeclado(texto => OnTextoLibreEnviado?.Invoke(texto));
    }

    /// <summary>Abre el teclado nativo del sistema para registrar el diagnóstico.
    /// Al confirmar dispara <see cref="OnDiagnosticoTextoEnviado"/>; al cancelar, nada.</summary>
    public void AbrirTecladoDiagnostico()
    {
        Debug.Log("[InteraccionPaciente] AbrirTecladoDiagnostico() llamado.", this);
        AbrirTeclado(texto => OnDiagnosticoTextoEnviado?.Invoke(texto));
    }

    /// <summary>Entrada directa de texto libre (tests / teclado diegético alterno).
    /// Equivale a confirmar el teclado de anamnesis.</summary>
    public void EnviarTextoLibre(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        OnTextoLibreEnviado?.Invoke(texto);
    }

    // Llamado desde los botones de examen generados dinámicamente (pinch).
    public void SolicitarExamen(string idExamen) =>
        OnExamenSolicitado?.Invoke(idExamen);

    // ── Teclado nativo ──────────────────────────────────────────────────────

    private void AbrirTeclado(Action<string> alConfirmar)
    {
        if (_teclado != null && _teclado.active)
        {
            Debug.Log("[InteraccionPaciente] AbrirTeclado: ya hay uno abierto (active=true); ignorado.", this);
            return;
        }

        Debug.Log($"[InteraccionPaciente] AbrirTeclado: TouchScreenKeyboard.isSupported={TouchScreenKeyboard.isSupported} " +
                  "→ llamando a TouchScreenKeyboard.Open()...", this);

        _alConfirmarTeclado = alConfirmar;
        _teclado = TouchScreenKeyboard.Open(
            "",
            TouchScreenKeyboardType.Default,
            autocorrection: false,
            multiline: false,
            secure: false,
            alert: false,
            textPlaceholder: "Escribí aquí…");
        _ultimoStatusLogueado = (TouchScreenKeyboard.Status)(-1); // fuerza el log de la 1ª transición

        Debug.Log($"[InteraccionPaciente] AbrirTeclado: Open() retornó. teclado={(_teclado != null ? "no-null" : "NULL")} " +
                  $"active={_teclado?.active} status={_teclado?.status}", this);
    }

    private void ProcesarTeclado()
    {
        if (_teclado == null) return;

        if (_teclado.status != _ultimoStatusLogueado)
        {
            Debug.Log($"[InteraccionPaciente] Teclado status → {_teclado.status} (active={_teclado.active})", this);
            _ultimoStatusLogueado = _teclado.status;
        }

        switch (_teclado.status)
        {
            case TouchScreenKeyboard.Status.Visible:
                return; // el usuario sigue escribiendo

            case TouchScreenKeyboard.Status.Done:
            {
                string texto = _teclado.text;
                Action<string> cb = _alConfirmarTeclado;
                DescartarTeclado();
                if (!string.IsNullOrWhiteSpace(texto)) cb?.Invoke(texto);
                return;
            }

            default: // Canceled / LostFocus → no dispara ningún evento
                DescartarTeclado();
                return;
        }
    }

    private void DescartarTeclado()
    {
        if (_teclado != null && _teclado.active) _teclado.active = false;
        _teclado = null;
        _alConfirmarTeclado = null;
        _ultimoStatusLogueado = (TouchScreenKeyboard.Status)(-1);
    }

    // ── Interno ──────────────────────────────────────────────────────────────

    private void DetectarProximidadManoRegion()
    {
        if (regionesAnatomicas == null) return;

        bool hayIzq = manoIzquierda != null;
        bool hayDer = manoDerecha != null;
        if (!hayIzq && !hayDer) return;

        Vector3 posIzq = hayIzq ? manoIzquierda.position : Vector3.zero;
        Vector3 posDer = hayDer ? manoDerecha.position   : Vector3.zero;

        for (int i = 0; i < regionesAnatomicas.Length; i++)
        {
            Transform region = regionesAnatomicas[i];
            if (region == null) continue;

            string nombre = _nombresRegion[i];

            Vector3 pos = region.position;
            bool cerca =
                (hayIzq && (posIzq - pos).sqrMagnitude <= _umbralProximidadSqr) ||
                (hayDer && (posDer - pos).sqrMagnitude <= _umbralProximidadSqr);

            if (!cerca) continue;

            // Throttle por región: no re-avisar hasta pasado el cooldown. Permite
            // re-examinar la misma región tantas veces como se quiera (a diferencia
            // de un dedupe permanente).
            float ultimoAviso;
            if (_ultimoAvisoRegion.TryGetValue(nombre, out ultimoAviso) &&
                Time.time - ultimoAviso < cooldownReexamenSeg)
                continue;

            _ultimoAvisoRegion[nombre] = Time.time;
            OnRegionExaminada?.Invoke(nombre);
        }
    }

    private void CachearNombresRegion()
    {
        int n = regionesAnatomicas != null ? regionesAnatomicas.Length : 0;
        if (_nombresRegion == null || _nombresRegion.Length != n)
            _nombresRegion = new string[n];

        for (int i = 0; i < n; i++)
            _nombresRegion[i] = regionesAnatomicas[i] != null ? regionesAnatomicas[i].name : null;
    }

    private void RecalcularUmbralSqr() =>
        _umbralProximidadSqr = umbralProximidadRegion * umbralProximidadRegion;
}
