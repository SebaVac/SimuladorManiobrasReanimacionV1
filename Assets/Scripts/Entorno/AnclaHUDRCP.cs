using UnityEngine;

/// <summary>Costado de la plataforma donde se colocan los paneles del HUD de RCP.</summary>
public enum LadoHudRCP
{
    /// <summary>El costado largo MÁS LEJANO a la cabeza al entrar en Simulación (los paneles quedan al otro lado de la plataforma).</summary>
    Opuesto,
    /// <summary>El extremo de la cabeza del paciente.</summary>
    Cabecera,
    /// <summary>El costado largo más cercano a la cabeza al entrar en Simulación.</summary>
    Cercano
}

/// <summary>
/// Ancla vacía que lleva el HUD de RCP (Panel_MetricasRCP, Panel_GuiaManos y el cronómetro) junto a la plataforma del
/// paciente, para poder leerlo SIN levantar la cabeza (al levantarla las manos salen del campo de las cámaras de
/// seguimiento y se pierde la compresión).
///
/// Sigue la POSICIÓN y el GIRO (yaw) de <see cref="SuperficiePaciente"/> en LateUpdate (sin escala, sin asignaciones, sin
/// sondeo: una sola escritura de pose por fotograma). Se ejecuta DESPUÉS de SuperficiePaciente (orden 50 contra 0), así
/// que el ancla nunca queda un fotograma atrasada. Los paneles son hijos del ancla y sus poses locales se calculan UNA
/// vez (al empezar y al pasar de Calibración a Simulación, vía <see cref="LogicaRCP.AlResetearEstado"/>); después quedan
/// fijos respecto de la plataforma, también cuando el paciente se mueve en Calibración.
///
/// Cada panel se coloca con un desfase en METROS en el marco del lado elegido (X a lo largo del borde, Y sobre la cara
/// superior de la plataforma, Z hacia afuera), mira hacia la cabeza (solo giro) y se inclina como un atril.
/// No toca el paciente, la lógica de compresión ni el calibrador.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class AnclaHUDRCP : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Tablero del paciente. Solo se usa su Transform (posición y giro); sus hijos no se tocan.")]
    [SerializeField] private SuperficiePaciente superficie;
    [Tooltip("Cabeza del usuario (CenterEyeAnchor). Se lee UNA vez al entrar en Simulación para elegir el lado y orientar los paneles.")]
    [SerializeField] private Transform cabeza;
    [Tooltip("Lógica de RCP. Se usa su evento AlResetearEstado y su bandera modoCalibracion para detectar la entrada en Simulación (sin sondeo).")]
    [SerializeField] private LogicaRCP logica;

    [Header("Paneles (hijos de esta ancla)")]
    [SerializeField] private Transform panelMetricas;
    [SerializeField] private Transform panelGuia;
    [Tooltip("Raíz del cronómetro de la sesión. Va en el grupo porque el cronómetro anclado a la cabeza queda fuera del cono de visión al mirar las manos.")]
    [SerializeField] private Transform panelCronometro;

    [Header("Lado")]
    [Tooltip("Opuesto = costado largo más lejano a la cabeza al entrar en Simulación; Cabecera = extremo de la cabeza del paciente; Cercano = costado largo más próximo a la cabeza. Se calcula una vez y queda fijo respecto de la plataforma.")]
    [SerializeField] private LadoHudRCP lado = LadoHudRCP.Opuesto;
    [Tooltip("true si la cabeza del paciente está hacia -X del marco de la plataforma (así está en SceneSimulador). Solo importa para el lado Cabecera.")]
    [SerializeField] private bool cabeceraEnMenosX = true;

    [Header("Desfases en metros (marco del lado: X a lo largo del borde, Y sobre la cara superior de la plataforma, Z hacia afuera)")]
    [SerializeField] private Vector3 desfaseMetricas = new Vector3(-0.63f, 0.42f, 0.80f);
    [SerializeField] private Vector3 desfaseGuia = new Vector3(0.01f, 0.42f, 0.80f);
    [Tooltip("Cronómetro: sobre los otros dos paneles, en el centro del grupo.")]
    [SerializeField] private Vector3 desfaseCronometro = new Vector3(-0.30f, 0.64f, 0.84f);

    [Header("Aspecto")]
    [Tooltip("Inclinación tipo atril, en grados: la cara del panel se inclina hacia arriba (hacia los ojos de quien comprime).")]
    [Range(0f, 80f)] [SerializeField] private float inclinacionAtrilGrados = 35f;
    [Tooltip("Factor sobre la escala original de Panel_MetricasRCP (1 = tamaño original de 1,24 m de ancho). Con 0,50 mide 0,62 m: unos 30° de ancho angular desde la posición de lectura.")]
    [Range(0.2f, 1f)] [SerializeField] private float factorEscalaMetricas = 0.50f;
    [Tooltip("Factor sobre la escala original de Panel_GuiaManos (1 = 1,14 m de ancho).")]
    [Range(0.2f, 1f)] [SerializeField] private float factorEscalaGuia = 0.50f;
    [Tooltip("Factor sobre la escala original del cronómetro (1 = 0,30 m de ancho).")]
    [Range(0.2f, 1.5f)] [SerializeField] private float factorEscalaCronometro = 0.80f;

    [Header("Criterios de lectura (para las mediciones; no actúan en tiempo de ejecución)")]
    [Tooltip("Ángulo máximo permitido entre la línea de los ojos al esternón y la de los ojos al centro de cada panel.")]
    [SerializeField] private float anguloMaximoAlEsternonGrados = 30f;
    [Tooltip("Ancho angular máximo de un panel visto desde la posición de lectura.")]
    [SerializeField] private float anchoAngularMaximoGrados = 35f;

    [Header("Paredes")]
    [Tooltip("Si el lado elegido dejaría algún panel fuera de la sala (según los límites de abajo), se prueba el siguiente lado de la lista Opuesto, Cabecera, Cercano. Se decide una sola vez, al calcular.")]
    [SerializeField] private bool evitarParedes = true;
    [Tooltip("Esquina mínima (X, Z) de la sala en metros, ya con margen (SceneSimulador: X -3,75 / Z -2,50).")]
    [SerializeField] private Vector2 limiteSalaMinimo = new Vector2(-3.55f, -2.30f);
    [Tooltip("Esquina máxima (X, Z) de la sala en metros, ya con margen (SceneSimulador: X 3,75 / Z 5,00).")]
    [SerializeField] private Vector2 limiteSalaMaximo = new Vector2(3.55f, 4.80f);

    /// <summary>Lado realmente usado en el último cálculo (puede diferir de <c>lado</c> si evitarParedes lo cambió).</summary>
    public LadoHudRCP LadoActivo { get; private set; }
    public float AnguloMaximoAlEsternonGrados => anguloMaximoAlEsternonGrados;
    public float AnchoAngularMaximoGrados => anchoAngularMaximoGrados;
    public float InclinacionAtrilGrados => inclinacionAtrilGrados;
    public Transform PanelMetricas => panelMetricas;
    public Transform PanelGuia => panelGuia;
    public Transform PanelCronometro => panelCronometro;

    [Header("Escalas originales (se guardan solas la primera vez; (0,0,0) = aún sin guardar)")]
    [Tooltip("Escala original de Panel_MetricasRCP antes de aplicar el factor. Así recalcular no acumula reducciones.")]
    [SerializeField] private Vector3 escalaOriginalMetricas;
    [Tooltip("Escala original de Panel_GuiaManos antes de aplicar el factor.")]
    [SerializeField] private Vector3 escalaOriginalGuia;
    [Tooltip("Escala original de la raíz del cronómetro antes de aplicar el factor.")]
    [SerializeField] private Vector3 escalaOriginalCronometro;

    private Vector3 _escalaBaseMetricas = Vector3.one, _escalaBaseGuia = Vector3.one, _escalaBaseCronometro = Vector3.one;
    private bool _escalasLeidas;
    private bool _enSimulacion;
    private bool _suscrito;

    void Awake()
    {
        LeerEscalasBase();
        Seguir();
    }

    void OnEnable()
    {
        if (logica != null && !_suscrito) { logica.AlResetearEstado += AlResetearEstado; _suscrito = true; }
        _enSimulacion = logica != null && !logica.modoCalibracion;
    }

    void OnDisable()
    {
        if (logica != null && _suscrito) { logica.AlResetearEstado -= AlResetearEstado; _suscrito = false; }
    }

    void Start() => Recalcular();

    void LateUpdate() => Seguir();

    // LogicaRCP.ResetearEstadoSimulacion lo lanza en cada cambio de modo (BotonMaestro fija modoCalibracion ANTES de llamarlo).
    // Solo se recalcula en el flanco Calibración -> Simulación: «Repetir sesión» no vuelve a elegir lado.
    private void AlResetearEstado()
    {
        bool sim = logica != null && !logica.modoCalibracion;
        if (sim && !_enSimulacion) Recalcular();
        _enSimulacion = sim;
    }

    private void LeerEscalasBase()
    {
        if (_escalasLeidas) return;
        if (escalaOriginalMetricas == Vector3.zero && panelMetricas != null) escalaOriginalMetricas = panelMetricas.localScale;
        if (escalaOriginalGuia == Vector3.zero && panelGuia != null) escalaOriginalGuia = panelGuia.localScale;
        if (escalaOriginalCronometro == Vector3.zero && panelCronometro != null) escalaOriginalCronometro = panelCronometro.localScale;
        _escalaBaseMetricas = escalaOriginalMetricas != Vector3.zero ? escalaOriginalMetricas : Vector3.one;
        _escalaBaseGuia = escalaOriginalGuia != Vector3.zero ? escalaOriginalGuia : Vector3.one;
        _escalaBaseCronometro = escalaOriginalCronometro != Vector3.zero ? escalaOriginalCronometro : Vector3.one;
        _escalasLeidas = true;
    }

    /// <summary>Copia la posición y el giro (yaw) del tablero. Sin escala y sin asignaciones.</summary>
    private void Seguir()
    {
        if (superficie == null) return;
        Transform t = superficie.transform;
        transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f));
    }

    /// <summary>Fuerza a releer las escalas originales (el builder lo llama tras tocar los campos).</summary>
    public void ReiniciarCacheDeEscalas() => _escalasLeidas = false;

    /// <summary>Elige el lado (según dónde está la cabeza AHORA) y recoloca los tres paneles. Público para las pruebas y el builder.</summary>
    public void Recalcular()
    {
        LeerEscalasBase();
        Seguir();
        if (superficie == null) return;

        // Cabeza en el marco local del ancla (rotado solo en yaw, origen en el centro de la cara superior del tablero)
        Vector3 cabezaLocal = cabeza != null ? transform.InverseTransformPoint(cabeza.position) : new Vector3(0f, 0f, -1f);
        float signo = cabezaLocal.z >= 0f ? 1f : -1f;   // lado donde está la cabeza a lo largo del eje Z local

        LadoHudRCP elegido = lado;
        if (evitarParedes)
        {
            if (!Cabe(lado, signo, cabezaLocal))
            {
                if (lado != LadoHudRCP.Opuesto && Cabe(LadoHudRCP.Opuesto, signo, cabezaLocal)) elegido = LadoHudRCP.Opuesto;
                else if (lado != LadoHudRCP.Cabecera && Cabe(LadoHudRCP.Cabecera, signo, cabezaLocal)) elegido = LadoHudRCP.Cabecera;
                else if (lado != LadoHudRCP.Cercano && Cabe(LadoHudRCP.Cercano, signo, cabezaLocal)) elegido = LadoHudRCP.Cercano;
                else Debug.LogWarning("[AnclaHUDRCP] Ningún lado deja el HUD dentro de la sala; se usa " + lado + ".", this);
                if (elegido != lado) Debug.Log("[AnclaHUDRCP] El lado " + lado + " dejaría el HUD fuera de la sala: se usa " + elegido + ".", this);
            }
        }
        LadoActivo = elegido;

        Colocar(panelMetricas, desfaseMetricas, _escalaBaseMetricas, factorEscalaMetricas, elegido, signo, cabezaLocal);
        Colocar(panelGuia, desfaseGuia, _escalaBaseGuia, factorEscalaGuia, elegido, signo, cabezaLocal);
        Colocar(panelCronometro, desfaseCronometro, _escalaBaseCronometro, factorEscalaCronometro, elegido, signo, cabezaLocal);
    }

    // Normal hacia afuera del lado, en el marco local del ancla (horizontal).
    private Vector3 NormalDelLado(LadoHudRCP l, float signoCabeza)
    {
        switch (l)
        {
            case LadoHudRCP.Cabecera: return new Vector3(cabeceraEnMenosX ? -1f : 1f, 0f, 0f);
            case LadoHudRCP.Cercano:  return new Vector3(0f, 0f, signoCabeza);
            default:                  return new Vector3(0f, 0f, -signoCabeza);
        }
    }

    // Posición local de un desfase: X a lo largo del borde (la normal girada +90° en Y), Y arriba, Z hacia afuera.
    private Vector3 PosicionLocal(Vector3 desfase, Vector3 normal)
    {
        Vector3 borde = new Vector3(normal.z, 0f, -normal.x);   // normal girada +90° alrededor de Y
        return borde * desfase.x + Vector3.up * desfase.y + normal * desfase.z;
    }

    private bool Cabe(LadoHudRCP l, float signo, Vector3 cabezaLocal)
    {
        Vector3 n = NormalDelLado(l, signo);
        return Cabe(panelMetricas, desfaseMetricas, n, _escalaBaseMetricas, factorEscalaMetricas)
            && Cabe(panelGuia, desfaseGuia, n, _escalaBaseGuia, factorEscalaGuia)
            && Cabe(panelCronometro, desfaseCronometro, n, _escalaBaseCronometro, factorEscalaCronometro);
    }

    // Un panel cabe si su centro, MÁS su semiancho (conservador: la mitad del ancho en cualquier dirección horizontal), queda dentro de la sala.
    private bool Cabe(Transform panel, Vector3 desfase, Vector3 normal, Vector3 escalaBase, float factor)
    {
        if (panel == null) return true;
        var rt = panel as RectTransform;
        float semi = rt != null ? 0.5f * rt.sizeDelta.x * escalaBase.x * factor : 0.2f;
        Vector3 mundo = transform.TransformPoint(PosicionLocal(desfase, normal));
        return mundo.x - semi >= limiteSalaMinimo.x && mundo.x + semi <= limiteSalaMaximo.x
            && mundo.z - semi >= limiteSalaMinimo.y && mundo.z + semi <= limiteSalaMaximo.y;
    }

    private void Colocar(Transform panel, Vector3 desfase, Vector3 escalaBase, float factor, LadoHudRCP l, float signo, Vector3 cabezaLocal)
    {
        if (panel == null) return;
        if (panel.parent != transform) panel.SetParent(transform, false);
        Vector3 pos = PosicionLocal(desfase, NormalDelLado(l, signo));

        // Mira hacia la cabeza (solo giro). El Canvas de mundo se lee desde su cara -Z, así que su +Z apunta AL REVÉS que la vista.
        Vector3 haciaCabeza = cabezaLocal - pos; haciaCabeza.y = 0f;
        if (haciaCabeza.sqrMagnitude < 1e-6f) haciaCabeza = -NormalDelLado(l, signo);
        Quaternion giro = Quaternion.LookRotation(-haciaCabeza.normalized, Vector3.up);
        Quaternion rot = giro * Quaternion.Euler(inclinacionAtrilGrados, 0f, 0f);   // +X inclina el +Z hacia abajo: la cara mira hacia arriba

        panel.SetLocalPositionAndRotation(pos, rot);
        panel.localScale = escalaBase * factor;
    }
}
