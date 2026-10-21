using System;
using UnityEngine;

public enum EstadoSesionRCP { EsperandoPrimeraCompresion, EnCurso, Terminada }

/// <summary>
/// Sesión cronometrada de RCP (SceneSimulador). Máquina de estados
/// EsperandoPrimeraCompresion -> EnCurso -> Terminada:
///  - El reloj arranca con el primer ciclo de push que registre <see cref="LogicaRCP"/>
///    (de cualquier profundidad), no al entrar a Simulación.
///  - Al agotarse <see cref="duracionSesionSeg"/> la sesión termina sola: se construye el
///    <see cref="ResultadoSesionRCP"/> una vez y LogicaRCP deja de evaluar compresiones.
///  - Cualquier ResetearEstadoSimulacion (cambio de modo, "Reiniciar sesión", repetir) DESCARTA
///    la sesión: contadores en cero y vuelta a EsperandoPrimeraCompresion, sin panel.
///
/// El tiempo se mide acumulando deltaTime solo cuando LogicaRCP no está pausada ni en
/// Calibración (nunca con Time.time). Solo contadores y acumuladores int/float por fotograma:
/// cero asignaciones. LogicaRCP no conoce esta clase (solo expone eventos).
/// </summary>
public class SesionRCP : MonoBehaviour
{
    [SerializeField] private LogicaRCP logica;

    [Header("Duración")]
    [Tooltip("Duración de la sesión en segundos (120 = 2 min). Bájalo a 5 para probar.")]
    [SerializeField] private float duracionSesionSeg = 120f;

    [Header("Frecuencia objetivo (cpm)")]
    [Tooltip("Espejo de GestorMetricasRCP.bpmMinimo/bpmMaximo (100-120/min, AHA 2020).")]
    [SerializeField] private float cpmMinimo = 100f;
    [SerializeField] private float cpmMaximo = 120f;

    public EstadoSesionRCP Estado { get; private set; } = EstadoSesionRCP.EsperandoPrimeraCompresion;
    public float DuracionSesionSeg => duracionSesionSeg;
    public float SegundosTranscurridos => _elapsed;
    public float SegundosRestantes => Mathf.Max(0f, duracionSesionSeg - _elapsed);
    /// <summary>Último resultado construido (válido mientras Estado == Terminada).</summary>
    public ResultadoSesionRCP UltimoResultado { get; private set; }

    /// <summary>Cambió el estado (incluye Terminada -> EsperandoPrimeraCompresion al descartar).</summary>
    public event Action<EstadoSesionRCP> AlCambiarEstado;
    /// <summary>La sesión terminó por tiempo; el resultado ya está construido.</summary>
    public event Action<ResultadoSesionRCP> AlTerminar;

    // ── Acumuladores (int/float; nada se asigna por fotograma) ──────────────
    private float _elapsed;
    private int _intentos, _insuf, _corr, _exc;
    private float _sumaProf;
    private int _intervalos, _intervalosEnRango;
    private float _sumaIntervalos, _tInicioAnterior;
    private bool _hayInicioAnterior;
    private float _segPostura;
    private int _salidas, _recoilEval, _recoilInc;
    private bool _ultimoCicloCorrecto; // el recoil se evalúa al iniciar el SIGUIENTE ciclo: pertenece al último cerrado

    private AudioClip _tonoFin;

    void Awake()
    {
        _tonoFin = CrearTonoFin(); // una vez, al arrancar (mismo patrón procedural que el bip del metrónomo)
    }

    void OnEnable()
    {
        if (logica == null) return;
        logica.AlIniciarCiclo   += OnIniciarCiclo;
        logica.AlCerrarCiclo    += OnCerrarCiclo;
        logica.AlEvaluarRecoil  += OnEvaluarRecoil;
        logica.AlSalirDeZona    += OnSalirDeZona;
        logica.AlResetearEstado += Descartar;
    }

    void OnDisable()
    {
        if (logica == null) return;
        logica.AlIniciarCiclo   -= OnIniciarCiclo;
        logica.AlCerrarCiclo    -= OnCerrarCiclo;
        logica.AlEvaluarRecoil  -= OnEvaluarRecoil;
        logica.AlSalirDeZona    -= OnSalirDeZona;
        logica.AlResetearEstado -= Descartar;
    }

    void Update()
    {
        if (logica == null || logica.modoCalibracion || logica.EstaPausado) return;
        AvanzarTiempo(Time.deltaTime);
    }

    /// <summary>Avanza el reloj de la sesión. Público para poder alimentarlo con pasos de
    /// tiempo conocidos (arnés de pruebas); en juego lo llama Update con deltaTime.</summary>
    public void AvanzarTiempo(float dt)
    {
        if (Estado != EstadoSesionRCP.EnCurso || dt <= 0f) return;

        float restante = duracionSesionSeg - _elapsed;
        if (dt > restante) dt = restante;
        _elapsed += dt;
        if (!logica.EnPosicionCorrecta) _segPostura += dt;

        if (_elapsed >= duracionSesionSeg) Terminar();
    }

    // ── Eventos de LogicaRCP ─────────────────────────────────────────────────

    private void OnIniciarCiclo()
    {
        if (Estado == EstadoSesionRCP.Terminada) return;
        if (Estado == EstadoSesionRCP.EsperandoPrimeraCompresion)
        {
            _elapsed = 0f;
            CambiarEstado(EstadoSesionRCP.EnCurso);
        }

        _intentos++;
        if (_hayInicioAnterior)
        {
            float intervalo = _elapsed - _tInicioAnterior;
            if (intervalo > 0f)
            {
                _intervalos++;
                _sumaIntervalos += intervalo;
                float cpm = 60f / intervalo;
                if (cpm >= cpmMinimo && cpm <= cpmMaximo) _intervalosEnRango++;
            }
        }
        _tInicioAnterior = _elapsed;
        _hayInicioAnterior = true;
    }

    private void OnCerrarCiclo(float profMaxCM)
    {
        if (Estado != EstadoSesionRCP.EnCurso) return;

        // Mismos umbrales y comparadores que EjecutarLogicaPush: <5 insuficiente, <=6 correcto, >6 excesivo.
        _ultimoCicloCorrecto = false;
        if (profMaxCM < LogicaRCP.PROF_MIN_CORRECTA_CM) _insuf++;
        else if (profMaxCM <= LogicaRCP.PROF_MAX_CORRECTA_CM) { _corr++; _ultimoCicloCorrecto = true; }
        else _exc++;
        _sumaProf += profMaxCM;
    }

    private void OnEvaluarRecoil(bool incompleto)
    {
        // LogicaRCP abre la ventana de recoil con su conteo legado (un ciclo excesivo que cruza 5-6 cm
        // también la abre). Para que la fila de receso sea coherente con "correctas", solo cuenta el
        // recoil de ciclos clasificados como correctos.
        if (Estado != EstadoSesionRCP.EnCurso || !_ultimoCicloCorrecto) return;
        _recoilEval++;
        if (incompleto) _recoilInc++;
    }

    private void OnSalirDeZona()
    {
        if (Estado == EstadoSesionRCP.EnCurso) _salidas++;
    }

    // ── Fin / descarte ───────────────────────────────────────────────────────

    private void Terminar()
    {
        // Primero se detiene LogicaRCP: cierra el ciclo abierto (OnCerrarCiclo aún en EnCurso).
        logica.DetenerPorFinDeSesion();

        var r = new ResultadoSesionRCP
        {
            duracionSeg = duracionSesionSeg,
            intentosTotales = _intentos,
            validas = _corr,
            validasConteoHud = logica.ContadorCompresiones,
            ciclosInsuficientes = _insuf,
            ciclosCorrectos = _corr,
            ciclosExcesivos = _exc,
            profundidadMediaCM = _intentos > 0 ? _sumaProf / _intentos : 0f,
            intervalos = _intervalos,
            intervalosEnRango = _intervalosEnRango,
            frecuenciaMediaCpm = _intervalos > 0 ? 60f * _intervalos / _sumaIntervalos : 0f,
            segundosPosturaIncorrecta = _segPostura,
            salidasDeZona = _salidas,
            recoilEvaluados = _recoilEval,
            recoilIncompletos = _recoilInc,
        };
        UltimoResultado = r;

        CambiarEstado(EstadoSesionRCP.Terminada);
        if (logica.altavoz != null && _tonoFin != null) logica.altavoz.PlayOneShot(_tonoFin);
        AlTerminar?.Invoke(r);
    }

    /// <summary>La sesión se descarta (salida por menú, reinicio, repetir): contadores en cero y
    /// vuelta a esperar la primera compresión, sin mostrar resultados.</summary>
    private void Descartar()
    {
        _elapsed = 0f;
        _intentos = _insuf = _corr = _exc = 0;
        _sumaProf = 0f;
        _intervalos = _intervalosEnRango = 0;
        _sumaIntervalos = _tInicioAnterior = 0f;
        _hayInicioAnterior = false;
        _segPostura = 0f;
        _salidas = _recoilEval = _recoilInc = 0;
        _ultimoCicloCorrecto = false;
        UltimoResultado = null;
        if (Estado != EstadoSesionRCP.EsperandoPrimeraCompresion)
            CambiarEstado(EstadoSesionRCP.EsperandoPrimeraCompresion);
    }

    private void CambiarEstado(EstadoSesionRCP nuevo)
    {
        Estado = nuevo;
        AlCambiarEstado?.Invoke(nuevo);
    }

    // Tono de fin de sesión: dos notas sinusoidales (660 Hz y 880 Hz), mismo patrón de AudioClip
    // procedural que el bip del metrónomo de LogicaRCP.
    private static AudioClip CrearTonoFin()
    {
        const int sampleRate = 44100;
        int notaLen = (int)(sampleRate * 0.18f);
        int length = notaLen * 2;
        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float f = i < notaLen ? 660f : 880f;
            int j = i < notaLen ? i : i - notaLen;
            float env = Mathf.Min(1f, (notaLen - j) / 1500f) * Mathf.Min(1f, j / 300f);
            samples[i] = 0.6f * Mathf.Sin(2f * Mathf.PI * f * i / sampleRate) * env;
        }
        var clip = AudioClip.Create("TonoFinSesionRCP", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
