using System;
using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// Coloca al jugador en el punto de inicio de la escena. Va en el OVRCameraRig. Espera a que el tracking de la
/// cabeza sea válido (evento <c>TrackingAcquired</c>; como máximo <see cref="tiempoMaximoEsperaSeg"/> s), espera
/// unos fotogramas para que la pose se estabilice y entonces mueve la RAÍZ del rig para que la cabeza quede sobre
/// el <see cref="PuntoInicioJugador"/> (XZ) mirando en su dirección (yaw, girando el rig alrededor del eje vertical
/// que pasa por la cabeza). No modifica la altura (Y) ni llama a RecenterPose: respeta el origen FloorLevel.
///
/// Se alinea una vez al cargar la escena y de nuevo cuando el usuario recentra (<c>RecenteredPose</c>). Sin sondeo
/// por fotograma: todo es por eventos y corrutinas con objetos de espera reutilizados. Tras alinear, recoloca una
/// vez los <see cref="AnclajeCabeza"/> activos y lanza <see cref="OnAlineado"/>.
/// </summary>
[DefaultExecutionOrder(-50)]
public class AlineadorInicioJugador : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Marcador de la escena que indica dónde debe quedar la cabeza y hacia dónde debe mirar.")]
    [SerializeField] private PuntoInicioJugador marcador;
    [Tooltip("Ancla de la cabeza (CenterEyeAnchor). Si queda vacía se toma del OVRCameraRig.")]
    [SerializeField] private Transform cabeza;
    [Tooltip("Raíz que se mueve y se gira. Si queda vacía se usa este mismo objeto (el OVRCameraRig).")]
    [SerializeField] private Transform raizRig;

    [Header("Espera de tracking")]
    [Tooltip("Segundos máximos esperando tracking válido. Pasado ese tiempo se alinea igualmente y se avisa UNA vez en la consola.")]
    [SerializeField] private float tiempoMaximoEsperaSeg = 1.5f;
    [Tooltip("Cada cuántos segundos se vuelve a comprobar la condición de tracking mientras se espera (solo durante la espera, no por fotograma).")]
    [SerializeField] private float intervaloSondeoSeg = 0.05f;
    [Tooltip("Fotogramas que se esperan tras recibir tracking (o tras un recentrado) antes de medir la cabeza.")]
    [SerializeField] private int fotogramasDeEstabilizacion = 2;

    [Header("Opciones")]
    [Tooltip("Realinear cuando el usuario recentra la vista (botón Oculus).")]
    [SerializeField] private bool realinearAlRecentrar = true;

    /// <summary>Se lanza después de cada alineación (inicial y por recentrado), con los AnclajeCabeza ya recolocados.</summary>
    public event Action OnAlineado;

    public bool Alineado { get; private set; }
    public int ConteoAlineaciones { get; private set; }
    public bool EsperandoTracking => _esperandoTracking;

    private ITrackingInicio _tracking;
    private bool _iniciado;
    private bool _esperandoTracking;
    private Coroutine _corrutinaAlinear;
    private Coroutine _corrutinaTimeout;
    private WaitForSecondsRealtime _esperaSondeo;
    private float _t0;                       // Time.realtimeSinceStartup al iniciar la espera
    private string _infoTracking = "n/d";    // condición de tracking cumplida y a los cuántos segundos (para el log)
    private Transform _trackingSpace;
    private OVRCameraRig _rigOVR;
    private readonly StringBuilder _sb = new StringBuilder(640);
    private readonly Action _alTrackingAdquirido;
    private readonly Action _alRecentrado;

    public AlineadorInicioJugador()
    {
        _alTrackingAdquirido = AlTrackingAdquirido;
        _alRecentrado = AlRecentrado;
    }

    void Start()
    {
        if (!_iniciado) Iniciar(new TrackingInicioOVR());
    }

    void OnDestroy()
    {
        if (_tracking != null) DesconectarTracking();
    }

    /// <summary>Arranca (o reinicia) la espera con la fuente de tracking indicada. Lo usa Start con la real y el
    /// banco de pruebas del Editor con la sintética. Solo para uso puntual, no por fotograma.</summary>
    public void Iniciar(ITrackingInicio tracking)
    {
        if (_tracking != null) DesconectarTracking();
        DetenerCorrutinas();
        _tracking = tracking;
        _iniciado = true;
        Alineado = false;
        _esperandoTracking = false;
        if (_esperaSondeo == null || Mathf.Abs(_esperaSondeo.waitTime - intervaloSondeoSeg) > 1e-4f)
            _esperaSondeo = new WaitForSecondsRealtime(Mathf.Max(0.01f, intervaloSondeoSeg));
        _t0 = Time.realtimeSinceStartup;
        _infoTracking = "n/d";

        ResolverReferencias();
        if (marcador == null || cabeza == null || raizRig == null)
        {
            Debug.LogWarning("[AlineadorInicioJugador] Falta el marcador, la cabeza o la raíz del rig: no se alinea al jugador.", this);
            return;
        }

        _tracking.TrackingAdquirido += _alTrackingAdquirido;
        _tracking.Recentrado += _alRecentrado;
        _tracking.Conectar();

        if (_tracking.PoseCabezaValida(cabeza))
        {
            RegistrarCondicion(_tracking.CondicionCumplida);
            _corrutinaAlinear = StartCoroutine(AlinarTrasEstabilizar(false));
        }
        else
        {
            _esperandoTracking = true;
            _corrutinaTimeout = StartCoroutine(EsperaTracking());
        }
    }

    private void ResolverReferencias()
    {
        if (raizRig == null) raizRig = transform;
        if (marcador == null) marcador = FindFirstObjectByType<PuntoInicioJugador>();
        if (cabeza == null)
        {
            var rig = GetComponent<OVRCameraRig>();
            if (rig != null) cabeza = rig.centerEyeAnchor;
            if (cabeza == null && Camera.main != null) cabeza = Camera.main.transform;
        }
        _rigOVR = GetComponent<OVRCameraRig>();
        _trackingSpace = _rigOVR != null ? _rigOVR.trackingSpace : null;
    }

    private void DesconectarTracking()
    {
        _tracking.TrackingAdquirido -= _alTrackingAdquirido;
        _tracking.Recentrado -= _alRecentrado;
        _tracking.Desconectar();
    }

    private void DetenerCorrutinas()
    {
        if (_corrutinaAlinear != null) { StopCoroutine(_corrutinaAlinear); _corrutinaAlinear = null; }
        if (_corrutinaTimeout != null) { StopCoroutine(_corrutinaTimeout); _corrutinaTimeout = null; }
    }

    // ── Eventos del tracking ─────────────────────────────────────────────────

    private void AlTrackingAdquirido()
    {
        if (!_esperandoTracking) return;
        _esperandoTracking = false;
        RegistrarCondicion("evento OVRManager.TrackingAcquired");
        if (_corrutinaTimeout != null) { StopCoroutine(_corrutinaTimeout); _corrutinaTimeout = null; }
        if (_corrutinaAlinear != null) StopCoroutine(_corrutinaAlinear);
        _corrutinaAlinear = StartCoroutine(AlinarTrasEstabilizar(false));
    }

    private void AlRecentrado()
    {
        if (!realinearAlRecentrar || _esperandoTracking) return;
        if (_corrutinaAlinear != null) StopCoroutine(_corrutinaAlinear);
        _corrutinaAlinear = StartCoroutine(AlinarTrasEstabilizar(true));
    }

    // ── Corrutinas ───────────────────────────────────────────────────────────

    /// <summary>Sondea la condición de tracking cada <see cref="intervaloSondeoSeg"/> s hasta cumplirla o agotar
    /// <see cref="tiempoMaximoEsperaSeg"/>. El sondeo existe porque el evento TrackingAcquired de OVRManager no se vuelve a
    /// disparar al cargar otra escena (su bandera wasPositionTracked es estática). El aviso se emite una sola vez.</summary>
    private IEnumerator EsperaTracking()
    {
        while (Time.realtimeSinceStartup - _t0 < tiempoMaximoEsperaSeg)
        {
            yield return _esperaSondeo;
            if (!_esperandoTracking) { _corrutinaTimeout = null; yield break; }        // llegó el evento y ya se encargó
            if (_tracking.PoseCabezaValida(cabeza))
            {
                _corrutinaTimeout = null;
                _esperandoTracking = false;
                RegistrarCondicion(_tracking.CondicionCumplida);
                _corrutinaAlinear = StartCoroutine(AlinarTrasEstabilizar(false));
                yield break;
            }
        }
        _corrutinaTimeout = null;
        if (!_esperandoTracking) yield break;
        _esperandoTracking = false;
        float seg = Time.realtimeSinceStartup - _t0;
        _infoTracking = "respaldo por tiempo agotado a los " + seg.ToString("0.00") + " s";
        Debug.LogWarning("[AlineadorInicioJugador] Sin tracking válido tras " + tiempoMaximoEsperaSeg.ToString("0.#")
                         + " s (¿sin visor?). Se alinea igualmente con la pose actual de la cabeza. Condiciones: "
                         + _tracking.Diagnosticar(cabeza), this);
        _corrutinaAlinear = StartCoroutine(AlinarTrasEstabilizar(false));
    }

    private void RegistrarCondicion(string condicion)
    {
        float seg = Time.realtimeSinceStartup - _t0;
        _infoTracking = condicion + " a los " + seg.ToString("0.00") + " s";
    }

    private IEnumerator AlinarTrasEstabilizar(bool porRecentrado)
    {
        for (int i = 0; i < fotogramasDeEstabilizacion; i++) yield return null;
        _corrutinaAlinear = null;
        Alinear(porRecentrado);
    }

    // ── Alineación ───────────────────────────────────────────────────────────

    /// <summary>Alinea ya (sin esperas). Público para el banco de pruebas y para reintentos manuales.</summary>
    public void Alinear(bool porRecentrado = false)
    {
        if (marcador == null || cabeza == null || raizRig == null) return;

        Vector3 antesPos = cabeza.position;
        float antesYaw = YawDe(cabeza);

        // 1) Giro alrededor del eje vertical que pasa por la cabeza: la cabeza no se mueve, solo su rumbo.
        float delta = Mathf.DeltaAngle(antesYaw, marcador.YawGrados);
        raizRig.RotateAround(cabeza.position, Vector3.up, delta);

        // 2) Traslación solo en XZ (la altura la da el suelo real con origen FloorLevel).
        Vector3 destino = marcador.PosicionCabeza;
        Vector3 p = cabeza.position;
        raizRig.position += new Vector3(destino.x - p.x, 0f, destino.z - p.z);

        Alineado = true;
        ConteoAlineaciones++;

        var anclajes = FindObjectsByType<AnclajeCabeza>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < anclajes.Length; i++) anclajes[i].Reposicionar();

        Vector3 d = cabeza.position;
        if (porRecentrado && _tracking.PoseCabezaValida(cabeza)) _infoTracking = "recentrado; " + _tracking.CondicionCumplida;
        _sb.Length = 0;
        _sb.Append("[AlineadorInicioJugador] ").Append(porRecentrado ? "Realineado tras recentrar" : "Alineado")
           .Append(" | cabeza antes (").Append(antesPos.x.ToString("0.00")).Append("; ").Append(antesPos.y.ToString("0.00")).Append("; ").Append(antesPos.z.ToString("0.00")).Append(") yaw ").Append(antesYaw.ToString("0"))
           .Append("° -> (").Append(d.x.ToString("0.00")).Append("; ").Append(d.y.ToString("0.00")).Append("; ").Append(d.z.ToString("0.00")).Append(") yaw ").Append(YawDe(cabeza).ToString("0"))
           .Append("° | altura de la cabeza ").Append(d.y.ToString("0.00")).Append(" m | origen: ").Append(_tracking.DescribirOrigen());
        // Campos añadidos (diagnóstico de la altura): no cambian el comportamiento
        _sb.Append(" | altura local de CenterEye ").Append(cabeza.localPosition.y.ToString("0.00")).Append(" m")
           .Append(" | Y mundial rig ").Append(raizRig.position.y.ToString("0.00")).Append(" m, TrackingSpace ");
        if (_trackingSpace != null) _sb.Append(_trackingSpace.position.y.ToString("0.00")).Append(" m"); else _sb.Append("n/d");
        _sb.Append(" | altura nodo EyeCenter ");
        if (_tracking.TryAlturaNodo(out float yNodo)) _sb.Append(yNodo.ToString("0.00")).Append(" m"); else _sb.Append("n/d");
        _sb.Append(" | lossyScale rig "); AgregarVector(_sb, raizRig.lossyScale);
        _sb.Append(", TrackingSpace "); if (_trackingSpace != null) AgregarVector(_sb, _trackingSpace.lossyScale); else _sb.Append("n/d");
        _sb.Append(", CenterEye "); AgregarVector(_sb, cabeza.lossyScale);
        _sb.Append(" | origen pedido ").Append(_tracking.DescribirOrigenPedido()).Append(", efectivo ").Append(_tracking.DescribirOrigenEfectivo())
           .Append(" | tracking: ").Append(_infoTracking);
        Debug.Log(_sb.ToString(), this);

        OnAlineado?.Invoke();
    }

    private static void AgregarVector(StringBuilder sb, Vector3 v)
    {
        sb.Append('(').Append(v.x.ToString("0.000")).Append("; ").Append(v.y.ToString("0.000")).Append("; ").Append(v.z.ToString("0.000")).Append(')');
    }

    private static float YawDe(Transform t)
    {
        Vector3 f = t.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f)
        {
            // Mirando casi vertical: el rumbo lo da el "arriba" de la cabeza (hacia delante si mira abajo, hacia atrás si mira arriba).
            f = t.forward.y < 0f ? t.up : -t.up;
            f.y = 0f;
        }
        return f.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }
}
