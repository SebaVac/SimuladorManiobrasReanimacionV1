using UnityEngine;
using TMPro;
using System.Text;

public class LogicaRCP : MonoBehaviour
{
    [Header("Control de Estado")]
    public bool modoCalibracion = true; // ✅ MANTENER MARCADO mientras ajustas el cubo con el calibrador

    [Header("Referencias OVR")]
    public OVRHand manoIzquierda;
    public OVRHand manoDerecha;
    public OVRSkeleton esqueletoIzq;
    public OVRSkeleton esqueletoDer;

    [Header("Interfaz (UI)")]
    public TextMeshPro txtInfoManos;
    public TextMeshPro txtInfoProfundidad;
    public TextMeshPro txtInfoRitmo;

    [Header("Referencias Visuales y Audio")]
    public Transform pechoVisual;
    public AudioSource altavoz;

    [Header("Calibración Manos")]
    public float umbralApertura = 0.07f;
    public float anguloMaximo = 60f;

    [Header("Configuración RCP (Zona Estricta)")]
    [Tooltip("Distancia horizontal máxima para considerar manos juntas (Entrada)")]
    public float toleranciaHorizontal = 0.20f;
    [Tooltip("Distancia horizontal para considerar que se SEPARARON (Salida - Mayor para evitar parpadeo)")]
    public float toleranciaHorizontalSalida = 0.25f;

    public float toleranciaVerticalManos = 0.15f;

    [Header("Zona de Contacto 3D (Cubo)")]
    [Tooltip("Radio del círculo horizontal alrededor del pecho")]
    public float radioEntrada = 0.15f;
    public float radioSalida = 0.25f;

    [Tooltip("Altura máxima permitida sobre el cubo para empezar (Eje Y)")]
    public float alturaMaximaEntrada = 0.15f;

    [Header("Mecánicas RCP")]
    public float alturaParaDetectarPush = 0.05f;
    public float hundimientoMaximoVisual = 0.06f;
    public float bpmMetronomo = 110f;
    [Tooltip("Profundidad (cm) por debajo de la cual se considera recoil completo. " +
             "Coincide con el umbral de salida de push (1.5 cm) por diseño.")]
    // Límite superior AHA 2020. Compresiones >6.0 cm aumentan riesgo de fracturas costales sin mejorar gasto cardíaco.
    public float umbralRecoilCM = 1.5f;

    // --- Estado de simulación ---
    private float alturaInicialManos;
    private float alturaInicialPechoY;
    private bool enPosicionCorrecta = false;
    private bool estaEmpujando = false;
    private int contadorCompresiones = 0;

    // --- Ritmo ---
    private float tiempoUltimaCompresion = 0f;
    private float bpmUsuario = 0f;
    private bool contadorSumadoEnEsteCiclo = false;

    // --- Metrónomo ---
    private float intervaloMetronomo;
    private float proximoTic;
    private AudioClip sonidoTic;

    // --- FIX 3: Umbrales cuadrados pre-calculados — elimina sqrt en Update ---
    private float _toleranciaHorizontalSqr;
    private float _toleranciaHorizontalSalidaSqr;
    private float _radioEntradaSqr;
    private float _radioSalidaSqr;

    // --- FIX 2: Caché de UI — elimina allocations de $"..." por frame ---
    private readonly StringBuilder _sbUI = new StringBuilder(64);
    private float _ultimaProfCMRound = float.MinValue;
    private int _ultimoContadorUI = -1;
    private bool _ultimoEstaEmpujandoUI = false;

    // --- FIX 1: Caché de tracking para detectar pérdida durante push ---
    private bool _trackingCompletoAnterior = false;

    // --- FIX 4: Guard para evitar ResetearEstadoSimulacion antes de Start ---
    private bool _inicializado = false;

    // --- Recoil (AHA 2020 — 5.º componente de calidad RCP) ---
    private bool esperandoRecoil = false;    // activo desde compresión válida hasta inicio del siguiente push
    private bool recoilAlcanzado = false;    // latch: true si profundidadCM bajó de umbralRecoilCM en la ventana
    private int contadorRecoilIncompleto = 0;
    private int _ultimoContadorRecoilUI = -1; // dirty flag UI

    // --- Datos expuestos para HUD externo (GestorMetricasRCP) ---
    private float profundidadActualCM = 0f;
    public float ProfundidadActualCM => profundidadActualCM;
    public float BpmActual => bpmUsuario;
    public bool EstaEmpujando => estaEmpujando;

    void Start()
    {
        if (pechoVisual != null) alturaInicialPechoY = pechoVisual.position.y;
        intervaloMetronomo = 60f / bpmMetronomo;
        sonidoTic = CrearSonidoBip();
        RecalcularUmbralesSqr();
        _inicializado = true;
    }

    void OnValidate()
    {
        // Mantiene los umbrales cuadrados sincronizados al editar valores en el Inspector
        RecalcularUmbralesSqr();
    }

    private void RecalcularUmbralesSqr()
    {
        _toleranciaHorizontalSqr        = toleranciaHorizontal        * toleranciaHorizontal;
        _toleranciaHorizontalSalidaSqr  = toleranciaHorizontalSalida  * toleranciaHorizontalSalida;
        _radioEntradaSqr                = radioEntrada                 * radioEntrada;
        _radioSalidaSqr                 = radioSalida                  * radioSalida;
    }

    void Update()
    {
        // ================================================================
        // 1. MODO CALIBRACIÓN
        // ================================================================
        if (modoCalibracion)
        {
            if (pechoVisual != null)
                alturaInicialPechoY = pechoVisual.position.y;

            ActualizarPanelManos("MODO CALIBRACIÓN\n(Ajusta y desmarca)", Color.cyan);
            LimpiarOtrosPaneles();
            return;
        }
        // ================================================================

        // 2. LÓGICA DE JUEGO NORMAL
        bool izqVisible = manoIzquierda.IsTracked;
        bool derVisible = manoDerecha.IsTracked;

        if (!enPosicionCorrecta)
        {
            if (!izqVisible || !derVisible || esqueletoIzq.Bones.Count == 0)
            {
                ActualizarPanelManos("Buscando manos...", Color.white);
                LimpiarOtrosPaneles();
                ResetearPecho();
                return;
            }

            if (!SonManosAbiertas()) { ActualizarPanelManos("ABRE LAS MANOS", Color.yellow); ResetearPecho(); return; }
            if (!EsRotacionCorrecta()) { ActualizarPanelManos("PALMAS AL SUELO", Color.yellow); ResetearPecho(); return; }
        }
        else
        {
            if (!izqVisible && !derVisible)
            {
                SalirDeModoRCP("MANOS PERDIDAS");
                return;
            }
        }

        ProcesarRCP(izqVisible, derVisible);
    }

    void ProcesarRCP(bool izqOk, bool derOk)
    {
        Vector3 posManos;
        bool trackingCompleto = izqOk && derOk;

        if (trackingCompleto)
        {
            posManos = (manoIzquierda.transform.position + manoDerecha.transform.position) / 2f;

            // FIX 3: sqrMagnitude en lugar de Vector2.Distance — sin sqrt por frame
            Vector3 delta = manoIzquierda.transform.position - manoDerecha.transform.position;
            float distManosHSqr = delta.x * delta.x + delta.z * delta.z;

            if (enPosicionCorrecta)
            {
                if (distManosHSqr > _toleranciaHorizontalSalidaSqr) { SalirDeModoRCP("JUNTA LAS MANOS"); return; }
            }
            else
            {
                if (distManosHSqr > _toleranciaHorizontalSqr) { ActualizarPanelManos("JUNTA LAS MANOS", Color.white); ResetearPecho(); return; }
            }
        }
        else
        {
            // Oclusión: usamos la mano visible
            posManos = izqOk ? manoIzquierda.transform.position : manoDerecha.transform.position;
        }

        // FIX 3: Comparación cuadrada para zona de contacto — sin sqrt
        float distHSqr = CalcularDistanciaHorizontalCuboSqr(posManos);
        float distanciaVerticalAlPecho = CalcularDistanciaVerticalCubo(posManos);

        if (!enPosicionCorrecta)
        {
            if (distHSqr > _radioEntradaSqr)
            {
                ActualizarPanelManos("ACÉRCATE AL CENTRO", Color.yellow);
                LimpiarOtrosPaneles();
                return;
            }

            if (distanciaVerticalAlPecho > alturaMaximaEntrada)
            {
                ActualizarPanelManos("BAJA LAS MANOS\n(Toca el pecho)", Color.yellow);
                LimpiarOtrosPaneles();
                return;
            }

            enPosicionCorrecta = true;
            alturaInicialManos = posManos.y;
            tiempoUltimaCompresion = Time.time;
            proximoTic = Time.time;
        }
        else
        {
            if (distHSqr > _radioSalidaSqr)
            {
                SalirDeModoRCP("TE ALEJASTE");
                return;
            }
        }

        ActualizarPanelManos("RCP EN PROCESO...", Color.cyan);
        EjecutarLogicaPush(posManos.y, trackingCompleto);
        _trackingCompletoAnterior = trackingCompleto; // FIX 1: actualizar caché para próximo frame
    }

    // --- CÁLCULOS MATEMÁTICOS ---

    // FIX 3: Retorna distancia al cuadrado — el llamador compara contra _radioXxxSqr
    float CalcularDistanciaHorizontalCuboSqr(Vector3 pos)
    {
        if (pechoVisual == null) return float.MaxValue;
        float dx = pos.x - pechoVisual.position.x;
        float dz = pos.z - pechoVisual.position.z;
        return dx * dx + dz * dz;
    }

    float CalcularDistanciaVerticalCubo(Vector3 pos)
    {
        if (pechoVisual == null) return 999f;
        return Mathf.Abs(pos.y - pechoVisual.position.y);
    }

    void SalirDeModoRCP(string motivo)
    {
        enPosicionCorrecta = false;
        estaEmpujando = false;
        _trackingCompletoAnterior = false; // FIX 1: limpiar caché de tracking
        esperandoRecoil = false;           // cancelar evaluación pendiente — contexto de sesión perdido
        ResetearPecho();
        ActualizarPanelManos(motivo, Color.white);
        LimpiarOtrosPaneles();
    }

    void EjecutarLogicaPush(float alturaActualManos, bool trackingCompleto)
    {
        // FIX 1: Protección de Delta Y ante pérdida de tracking durante un push.
        // Si el frame anterior teníamos ambas manos y ahora perdemos una mientras
        // estábamos empujando, la posición de la mano restante puede ser inconsistente
        // con alturaInicialManos (punto de referencia del push). Recalibramos la línea
        // base con la posición actual y salimos limpiamente del estado de push.
        if (!trackingCompleto && _trackingCompletoAnterior && estaEmpujando)
        {
            alturaInicialManos = alturaActualManos;
            estaEmpujando = false;
            contadorSumadoEnEsteCiclo = false;
            esperandoRecoil = false; // C1: cancelar sin confirmar — baseline recalibrado por pérdida de tracking, no por compresión real
        }

        if (Time.time >= proximoTic)
        {
            if (altavoz != null && sonidoTic != null) altavoz.PlayOneShot(sonidoTic);
            proximoTic = Time.time + intervaloMetronomo;
        }

        float profundidad = alturaInicialManos - alturaActualManos;
        float profundidadCM = profundidad * 100f;
        profundidadActualCM = profundidadCM;

        if (pechoVisual != null)
        {
            float hundimientoVisual = Mathf.Clamp(profundidad, 0f, hundimientoMaximoVisual);
            Vector3 nuevaPos = pechoVisual.position;
            nuevaPos.y = alturaInicialPechoY - hundimientoVisual;
            pechoVisual.position = nuevaPos;
        }

        // Determinar color y registrar compresión usando el estado PRE-transición
        // (comportamiento idéntico al original: el texto refleja el estado del frame actual)
        Color colorProf = Color.white;
        if (estaEmpujando)
        {
            if (profundidadCM < 5f)
            {
                colorProf = Color.yellow;
            }
            else if (profundidadCM <= 6.0f)
            {
                colorProf = Color.green;
                if (!contadorSumadoEnEsteCiclo)
                {
                    RegistrarCompresion();
                    contadorSumadoEnEsteCiclo = true;
                    esperandoRecoil = true;  // abre ventana de recoil
                    recoilAlcanzado = false; // latch: se activará cuando la mano suba por encima del umbral
                }
            }
            else
            {
                colorProf = Color.red;
            }
        }
        else
        {
            contadorSumadoEnEsteCiclo = false;

            // Actualizar latch de recoil sólo con tracking completo.
            // Si trackingCompleto = false, suspendemos la evaluación ese frame (evita falso positivo por oclusión).
            if (esperandoRecoil && trackingCompleto && !recoilAlcanzado)
            {
                if (profundidadCM < umbralRecoilCM)
                    recoilAlcanzado = true;
            }
        }

        // Capturar estado de UI antes de la transición (idéntico al original)
        bool estaEmpujandoParaUI = estaEmpujando;

        if (profundidad > 0.02f && !estaEmpujando)
        {
            // Evaluar recoil ANTES de iniciar el siguiente push (diseño latch)
            if (esperandoRecoil)
            {
                if (!recoilAlcanzado) contadorRecoilIncompleto++;
                esperandoRecoil = false;
            }
            estaEmpujando = true;
        }
        else if (profundidad < 0.015f && estaEmpujando)
        {
            estaEmpujando = false;
        }

        // FIX 2: Dirty flag — sólo reconstruir la UI cuando los valores realmente cambian.
        // Elimina la generación de strings con $"..." cada frame (90 allocs/seg en Quest).
        // SetText(StringBuilder) de TMP es zero-alloc para el componente de texto.
        if (txtInfoProfundidad != null)
        {
            float profRound = Mathf.Round(profundidadCM * 10f); // resolución 0.1 cm
            if (profRound != _ultimaProfCMRound
                || contadorCompresiones != _ultimoContadorUI
                || estaEmpujandoParaUI != _ultimoEstaEmpujandoUI)
            {
                _ultimaProfCMRound       = profRound;
                _ultimoContadorUI        = contadorCompresiones;
                _ultimoEstaEmpujandoUI   = estaEmpujandoParaUI;

                _sbUI.Clear();
                _sbUI.Append("Total: ").Append(contadorCompresiones).Append('\n');

                if (estaEmpujandoParaUI)
                {
                    // ToString("F1") asigna 1 string sólo cuando el valor cambia,
                    // no en cada frame — reducción de ~90x en la frecuencia de alloc.
                    string valStr = profundidadCM.ToString("F1");
                    if (profundidadCM < 5f)
                        _sbUI.Append("▼ ").Append(valStr).Append(" cm ▼\n(EMPUJA MÁS)");
                    else if (profundidadCM <= 6.0f)
                        _sbUI.Append("★ ").Append(valStr).Append(" cm ★\n(PERFECTO)");
                    else
                        _sbUI.Append("🛑 ").Append(valStr).Append(" cm 🛑\n(TE PASASTE)");
                }
                else
                {
                    _sbUI.Append("▲ SUBE ▲");
                }

                txtInfoProfundidad.SetText(_sbUI);
                txtInfoProfundidad.color = colorProf;
            }
        }

        // Recoil UI — dirty flag: sólo actualiza cuando el conteo cambia.
        // Reutiliza _sbUI (ya fue consumido por SetText arriba, sin referencias pendientes).
        if (txtInfoRitmo != null && contadorRecoilIncompleto != _ultimoContadorRecoilUI)
        {
            _ultimoContadorRecoilUI = contadorRecoilIncompleto;
            if (contadorRecoilIncompleto > 0)
            {
                _sbUI.Clear();
                _sbUI.Append("⚠ Recoil incompleto: ").Append(contadorRecoilIncompleto);
                txtInfoRitmo.SetText(_sbUI);
                txtInfoRitmo.color = Color.yellow;
            }
            else
            {
                txtInfoRitmo.text = "";
            }
        }
    }

    // FIX 4: Limpieza pública de estado interno para BotonMaestro.
    // Evita que datos sucios de una sesión anterior contaminen la siguiente.
    public void ResetearEstadoSimulacion()
    {
        if (!_inicializado) return; // Guard: no ejecutar si Start() aún no corrió

        enPosicionCorrecta      = false;
        estaEmpujando           = false;
        contadorCompresiones    = 0;
        contadorSumadoEnEsteCiclo = false;
        bpmUsuario              = 0f;
        alturaInicialManos      = 0f;
        _trackingCompletoAnterior = false;

        // Invalidar caché de UI para forzar redibujado completo en la próxima actualización
        _ultimaProfCMRound      = float.MinValue;
        _ultimoContadorUI       = -1;
        _ultimoEstaEmpujandoUI  = false;

        tiempoUltimaCompresion  = Time.time;
        proximoTic              = Time.time + intervaloMetronomo;

        // Resetear el pecho a la última posición calibrada conocida, luego sincronizar
        ResetearPecho();
        if (pechoVisual != null) alturaInicialPechoY = pechoVisual.position.y;

        // Recoil
        esperandoRecoil          = false;
        recoilAlcanzado          = false;
        contadorRecoilIncompleto = 0;
        _ultimoContadorRecoilUI  = -1;
        if (txtInfoRitmo != null) txtInfoRitmo.text = "";
    }

    // --- AUXILIARES ---
    void ActualizarPanelManos(string texto, Color color) { if (txtInfoManos != null) { txtInfoManos.text = texto; txtInfoManos.color = color; } }

    void LimpiarOtrosPaneles()
    {
        if (txtInfoProfundidad != null)
        {
            txtInfoProfundidad.text = "--";
            _ultimaProfCMRound = float.MinValue; // FIX 2: invalidar caché para forzar redibujado al re-entrar
        }
        if (txtInfoRitmo != null)
        {
            txtInfoRitmo.text = "";
            _ultimoContadorRecoilUI = -1; // forzar redibujado al re-entrar en zona RCP
        }
    }

    void RegistrarCompresion() { contadorCompresiones++; float tiempoActual = Time.time; float diferencia = tiempoActual - tiempoUltimaCompresion; if (diferencia > 0) { float nuevoBPM = 60f / diferencia; if (bpmUsuario == 0) bpmUsuario = nuevoBPM; else bpmUsuario = Mathf.Lerp(bpmUsuario, nuevoBPM, 0.3f); } tiempoUltimaCompresion = tiempoActual; }
    void ResetearPecho() { if (pechoVisual != null && Mathf.Abs(pechoVisual.position.y - alturaInicialPechoY) > 0.001f) { Vector3 pos = pechoVisual.position; pos.y = alturaInicialPechoY; pechoVisual.position = pos; } }
    AudioClip CrearSonidoBip() { int sampleRate = 44100; float duracion = 0.1f; int length = (int)(sampleRate * duracion); float[] samples = new float[length]; float frecuencia = 1000f; for (int i = 0; i < length; i++) { samples[i] = Mathf.Sin(2 * Mathf.PI * frecuencia * i / sampleRate); if (i > length - 1000) samples[i] *= (length - i) / 1000f; } AudioClip clip = AudioClip.Create("BipProcedural", length, 1, sampleRate, false); clip.SetData(samples, 0); return clip; }
    bool SonManosAbiertas() { float apIzq = ObtenerApertura(esqueletoIzq); float apDer = ObtenerApertura(esqueletoDer); return (apIzq > umbralApertura && apDer > umbralApertura); }
    bool EsRotacionCorrecta() { float angIzq = Vector3.Angle(manoIzquierda.transform.up, Vector3.up); float angDer = Vector3.Angle(manoDerecha.transform.up, Vector3.up); return (angIzq < anguloMaximo && angDer < anguloMaximo); }
    float ObtenerApertura(OVRSkeleton esqueleto) { return Vector3.Distance(esqueleto.Bones[0].Transform.position, esqueleto.Bones[(int)OVRSkeleton.BoneId.Hand_Index3].Transform.position); }
}
