using UnityEngine;
using TMPro;

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

    public float toleranciaVerticalManos = 0.15f; // Diferencia de altura entre mano izq y der

    [Header("Zona de Contacto 3D (Cubo)")]
    [Tooltip("Radio del círculo horizontal alrededor del pecho")]
    public float radioEntrada = 0.15f; // 15cm de radio (Más estricto)
    public float radioSalida = 0.25f;  // 25cm para salir

    [Tooltip("Altura máxima permitida sobre el cubo para empezar (Eje Y)")]
    public float alturaMaximaEntrada = 0.15f; // Tienes que estar a menos de 15cm de altura del pecho

    [Header("Mecánicas RCP")]
    public float alturaParaDetectarPush = 0.05f;
    public float hundimientoMaximoVisual = 0.06f;
    public float bpmMetronomo = 110f;

    // Variables internas
    private float alturaInicialManos;
    private float alturaInicialPechoY;
    private bool enPosicionCorrecta = false;
    private bool estaEmpujando = false;
    private int contadorCompresiones = 0;

    // Ritmo
    private float tiempoUltimaCompresion = 0f;
    private float bpmUsuario = 0f;
    private bool contadorSumadoEnEsteCiclo = false;

    // Metrónomo
    private float intervaloMetronomo;
    private float proximoTic;
    private AudioClip sonidoTic;

    void Start()
    {
        if (pechoVisual != null) alturaInicialPechoY = pechoVisual.position.y;
        intervaloMetronomo = 60f / bpmMetronomo;
        sonidoTic = CrearSonidoBip();
    }

    void Update()
    {
        // ================================================================
        // 1. MODO CALIBRACIÓN (NUEVO)
        // ================================================================
        if (modoCalibracion)
        {
            // Mientras esto esté activo, el script "aprende" la nueva altura del cubo
            // en lugar de obligarlo a volver a la posición original.
            if (pechoVisual != null)
            {
                alturaInicialPechoY = pechoVisual.position.y;
            }

            // Feedback visual para que sepas que el juego está en pausa
            ActualizarPanelManos("MODO CALIBRACIÓN\n(Ajusta y desmarca)", Color.cyan);
            LimpiarOtrosPaneles();

            // IMPORTANTE: Cortamos aquí para que no ejecute lógica de juego ni resetee nada.
            return;
        }
        // ================================================================

        // 2. LÓGICA DE JUEGO NORMAL
        bool izqVisible = manoIzquierda.IsTracked;
        bool derVisible = manoDerecha.IsTracked;

        if (!enPosicionCorrecta)
        {
            // FASE DE ENTRADA: Exigimos ver todo perfecto
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
            // FASE DE MANTENIMIENTO: Solo salimos si perdemos ambas
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

        // --- A. CALCULAR POSICIÓN Y VERIFICAR UNIÓN DE MANOS ---
        if (izqOk && derOk)
        {
            posManos = (manoIzquierda.transform.position + manoDerecha.transform.position) / 2;

            // Distancia entre manos (Horizontal)
            float distManosH = Vector2.Distance(new Vector2(manoIzquierda.transform.position.x, manoIzquierda.transform.position.z),
                                                new Vector2(manoDerecha.transform.position.x, manoDerecha.transform.position.z));

            // Lógica de Histéresis para manos juntas
            if (enPosicionCorrecta)
            {
                // Si ya estamos dentro, somos más permisivos (toleranciaHorizontalSalida)
                if (distManosH > toleranciaHorizontalSalida) { SalirDeModoRCP("JUNTA LAS MANOS"); return; }
            }
            else
            {
                // Si estamos fuera, somos estrictos (toleranciaHorizontal)
                if (distManosH > toleranciaHorizontal) { ActualizarPanelManos("JUNTA LAS MANOS", Color.white); ResetearPecho(); return; }
            }
        }
        else
        {
            // Oclusión: Usamos la mano visible
            posManos = izqOk ? manoIzquierda.transform.position : manoDerecha.transform.position;
        }

        // --- B. VERIFICAR ZONA 3D (Cubo) ---
        float distanciaHorizontalAlPecho = CalcularDistanciaHorizontalCubo(posManos);
        float distanciaVerticalAlPecho = CalcularDistanciaVerticalCubo(posManos);

        if (!enPosicionCorrecta)
        {
            // --- CONDICIONES PARA ENTRAR (ESTRICTAS) ---

            // 1. Chequeo Horizontal (GPS)
            if (distanciaHorizontalAlPecho > radioEntrada)
            {
                ActualizarPanelManos("ACÉRCATE AL CENTRO", Color.yellow);
                LimpiarOtrosPaneles();
                return;
            }

            // 2. Chequeo Vertical (Altura)
            if (distanciaVerticalAlPecho > alturaMaximaEntrada)
            {
                ActualizarPanelManos("BAJA LAS MANOS\n(Toca el pecho)", Color.yellow);
                LimpiarOtrosPaneles();
                return;
            }

            // ¡Si pasamos ambos filtros, entramos!
            enPosicionCorrecta = true;
            alturaInicialManos = posManos.y;
            tiempoUltimaCompresion = Time.time;
            proximoTic = Time.time;
        }
        else
        {
            // --- CONDICIONES PARA SALIR (PERMISIVAS) ---
            // Solo salimos si te alejas mucho horizontalmente
            if (distanciaHorizontalAlPecho > radioSalida)
            {
                SalirDeModoRCP("TE ALEJASTE");
                return;
            }
        }

        // --- C. EJECUTAR LÓGICA ---
        ActualizarPanelManos("RCP EN PROCESO...", Color.cyan);
        EjecutarLogicaPush(posManos.y);
    }

    // --- CÁLCULOS MATEMÁTICOS ---

    float CalcularDistanciaHorizontalCubo(Vector3 pos)
    {
        if (pechoVisual == null) return 999f;
        return Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pechoVisual.position.x, pechoVisual.position.z));
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
        ResetearPecho();
        ActualizarPanelManos(motivo, Color.white);
        LimpiarOtrosPaneles();
    }

    void EjecutarLogicaPush(float alturaActualManos)
    {
        if (Time.time >= proximoTic)
        {
            if (altavoz != null && sonidoTic != null) altavoz.PlayOneShot(sonidoTic);
            proximoTic = Time.time + intervaloMetronomo;
        }

        float profundidad = alturaInicialManos - alturaActualManos;
        float profundidadCM = profundidad * 100f;

        if (pechoVisual != null)
        {
            float hundimientoVisual = Mathf.Clamp(profundidad, 0f, hundimientoMaximoVisual);
            Vector3 nuevaPos = pechoVisual.position;
            nuevaPos.y = alturaInicialPechoY - hundimientoVisual;
            pechoVisual.position = nuevaPos;
        }

        string txtProf = "";
        Color colorProf = Color.white;

        if (estaEmpujando)
        {
            if (profundidadCM < 5f) { txtProf = $"▼ {profundidadCM:F1} cm ▼\n(EMPUJA MÁS)"; colorProf = Color.yellow; }
            else if (profundidadCM <= 6.5f)
            {
                txtProf = $"★ {profundidadCM:F1} cm ★\n(PERFECTO)"; colorProf = Color.green;
                if (!contadorSumadoEnEsteCiclo) { RegistrarCompresion(); contadorSumadoEnEsteCiclo = true; }
            }
            else { txtProf = $"🛑 {profundidadCM:F1} cm 🛑\n(TE PASASTE)"; colorProf = Color.red; }
        }
        else
        {
            txtProf = "▲ SUBE ▲"; colorProf = Color.white;
            contadorSumadoEnEsteCiclo = false;
        }

        if (profundidad > 0.02f && !estaEmpujando) estaEmpujando = true;
        else if (profundidad < 0.015f && estaEmpujando) estaEmpujando = false;

        if (txtInfoProfundidad != null) { txtInfoProfundidad.text = $"Total: {contadorCompresiones}\n{txtProf}"; txtInfoProfundidad.color = colorProf; }
    }

    // --- RESTO DE AUXILIARES ---
    void ActualizarPanelManos(string texto, Color color) { if (txtInfoManos != null) { txtInfoManos.text = texto; txtInfoManos.color = color; } }
    void LimpiarOtrosPaneles() { if (txtInfoProfundidad != null) txtInfoProfundidad.text = "--"; if (txtInfoRitmo != null) txtInfoRitmo.text = ""; }
    void RegistrarCompresion() { contadorCompresiones++; float tiempoActual = Time.time; float diferencia = tiempoActual - tiempoUltimaCompresion; if (diferencia > 0) { float nuevoBPM = 60f / diferencia; if (bpmUsuario == 0) bpmUsuario = nuevoBPM; else bpmUsuario = Mathf.Lerp(bpmUsuario, nuevoBPM, 0.3f); } tiempoUltimaCompresion = tiempoActual; }

    // El ResetearPecho ahora solo actúa si NO estamos calibrando (gracias al return del Update)
    void ResetearPecho() { if (pechoVisual != null && Mathf.Abs(pechoVisual.position.y - alturaInicialPechoY) > 0.001f) { Vector3 pos = pechoVisual.position; pos.y = alturaInicialPechoY; pechoVisual.position = pos; } }

    AudioClip CrearSonidoBip() { int sampleRate = 44100; float duracion = 0.1f; int length = (int)(sampleRate * duracion); float[] samples = new float[length]; float frecuencia = 1000f; for (int i = 0; i < length; i++) { samples[i] = Mathf.Sin(2 * Mathf.PI * frecuencia * i / sampleRate); if (i > length - 1000) samples[i] *= (length - i) / 1000f; } AudioClip clip = AudioClip.Create("BipProcedural", length, 1, sampleRate, false); clip.SetData(samples, 0); return clip; }
    bool SonManosAbiertas() { float apIzq = ObtenerApertura(esqueletoIzq); float apDer = ObtenerApertura(esqueletoDer); return (apIzq > umbralApertura && apDer > umbralApertura); }
    bool EsRotacionCorrecta() { float angIzq = Vector3.Angle(manoIzquierda.transform.up, Vector3.up); float angDer = Vector3.Angle(manoDerecha.transform.up, Vector3.up); return (angIzq < anguloMaximo && angDer < anguloMaximo); }
    float ObtenerApertura(OVRSkeleton esqueleto) { return Vector3.Distance(esqueleto.Bones[0].Transform.position, esqueleto.Bones[(int)OVRSkeleton.BoneId.Hand_Index3].Transform.position); }
}