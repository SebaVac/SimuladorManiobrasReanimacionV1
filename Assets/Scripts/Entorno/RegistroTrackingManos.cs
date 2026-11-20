using System.Text;
using UnityEngine;

/// <summary>
/// DIAGNÓSTICO TEMPORAL (root DEBUG_TRACKING_TEMPORAL): registra en logcat, con el prefijo [TRACK], cada vez que una mano
/// pasa a NO rastreada o vuelve a estarlo durante la Simulación de RCP, para medir cuánto se pierde la compresión al levantar
/// la cabeza a leer el HUD. No modifica nada: solo lee (OVRHand.IsTracked, OVRSkeleton.IsDataValid, la cabeza, el esternón y la
/// sesión). Los Debug.Log ocurren SOLO en los flancos y al terminar la sesión; el resto del tiempo no se asigna memoria
/// (referencias cacheadas, StringBuilder reutilizado). Se quita con el menú Tools/RCP/Quitar registro de tracking temporal.
/// </summary>
[DisallowMultipleComponent]
public class RegistroTrackingManos : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private LogicaRCP logica;
    [SerializeField] private SesionRCP sesion;
    [Tooltip("Cabeza del usuario (CenterEyeAnchor).")]
    [SerializeField] private Transform cabeza;
    [Tooltip("Esternón (el cubo PechoVisual / sensor de pecho).")]
    [SerializeField] private Transform esternon;
    [SerializeField] private OVRHand manoIzquierda;
    [SerializeField] private OVRHand manoDerecha;
    [SerializeField] private OVRSkeleton esqueletoIzquierdo;
    [SerializeField] private OVRSkeleton esqueletoDerecho;
    [Tooltip("Paneles del HUD (centros) para medir el ángulo de la mirada al panel más cercano.")]
    [SerializeField] private Transform[] paneles;

    private const int TRAMOS = 10;                       // 0-10°, 10-20°, ..., 80°+ de pitch hacia abajo
    private readonly int[] _perdidasPorTramo = new int[TRAMOS];
    private readonly float[] _segPerdidosPorTramo = new float[TRAMOS];
    private readonly StringBuilder _sb = new StringBuilder(320);
    private bool _rastreaIzq = true, _rastreaDer = true;
    private float _inicioPerdidaIzq, _inicioPerdidaDer;
    private int _tramoPerdidaIzq, _tramoPerdidaDer;
    private bool _enSimulacionPrevio, _resumenEmitido;

    void OnEnable() { if (sesion != null) sesion.AlTerminar += AlTerminarSesion; }
    void OnDisable() { if (sesion != null) sesion.AlTerminar -= AlTerminarSesion; }

    void Update()
    {
        bool sim = logica != null && !logica.modoCalibracion && !logica.EstaPausado;
        if (!sim) { _enSimulacionPrevio = false; return; }
        if (!_enSimulacionPrevio)
        {
            _enSimulacionPrevio = true; _resumenEmitido = false;
            _rastreaIzq = Rastreada(manoIzquierda, esqueletoIzquierdo); _rastreaDer = Rastreada(manoDerecha, esqueletoDerecho);
            System.Array.Clear(_perdidasPorTramo, 0, TRAMOS); System.Array.Clear(_segPerdidosPorTramo, 0, TRAMOS);
            return;
        }
        Revisar(manoIzquierda, esqueletoIzquierdo, "Izquierda", ref _rastreaIzq, ref _inicioPerdidaIzq, ref _tramoPerdidaIzq);
        Revisar(manoDerecha, esqueletoDerecho, "Derecha", ref _rastreaDer, ref _inicioPerdidaDer, ref _tramoPerdidaDer);
    }

    private static bool Rastreada(OVRHand mano, OVRSkeleton esqueleto)
    {
        return mano != null && mano.IsTracked && (esqueleto == null || esqueleto.IsDataValid);
    }

    private float PitchAbajo()
    {
        if (cabeza == null) return 0f;
        float x = cabeza.eulerAngles.x; if (x > 180f) x -= 360f;   // + = mirando hacia abajo
        return x;
    }

    private void Revisar(OVRHand mano, OVRSkeleton esqueleto, string nombre, ref bool rastreaba, ref float inicio, ref int tramo)
    {
        bool ahora = Rastreada(mano, esqueleto);
        if (ahora == rastreaba) return;
        float t = sesion != null ? sesion.SegundosTranscurridos : Time.time;
        if (!ahora)
        {
            inicio = Time.realtimeSinceStartup;
            tramo = Mathf.Clamp(Mathf.FloorToInt(PitchAbajo() / 10f), 0, TRAMOS - 1);
            _perdidasPorTramo[tramo]++;
            Escribir(t, nombre, "PERDIDA", 0f);
        }
        else
        {
            float dur = Time.realtimeSinceStartup - inicio;
            _segPerdidosPorTramo[tramo] += dur;
            Escribir(t, nombre, "RECUPERADA", dur);
        }
        rastreaba = ahora;
    }

    private void Escribir(float tSesion, string mano, string evento, float duracion)
    {
        Vector3 e = cabeza != null ? cabeza.position : Vector3.zero;
        Vector3 haciaEsternon = esternon != null ? esternon.position - e : Vector3.forward;
        float angMirada = cabeza != null ? Vector3.Angle(cabeza.forward, haciaEsternon) : 0f;
        float angPanel = -1f;
        if (cabeza != null && paneles != null)
            for (int i = 0; i < paneles.Length; i++)
            {
                if (paneles[i] == null || !paneles[i].gameObject.activeInHierarchy) continue;
                float a = Vector3.Angle(cabeza.forward, paneles[i].position - e);
                if (angPanel < 0f || a < angPanel) angPanel = a;
            }
        _sb.Length = 0;
        _sb.Append("[TRACK] t=").Append(tSesion.ToString("0.00")).Append(" s mano=").Append(mano).Append(' ').Append(evento);
        if (duracion > 0f) _sb.Append(" perdida=").Append(duracion.ToString("0.00")).Append(" s");
        _sb.Append(" pitchCabeza=").Append(PitchAbajo().ToString("0")).Append("° mirada-esternón=").Append(angMirada.ToString("0"))
           .Append("° distOjos-esternón=").Append(haciaEsternon.magnitude.ToString("0.00")).Append(" m mirada-panelMasCercano=")
           .Append(angPanel >= 0f ? angPanel.ToString("0") + "°" : "n/d");
        Debug.Log(_sb.ToString(), this);
    }

    private void AlTerminarSesion(ResultadoSesionRCP r)
    {
        if (_resumenEmitido) return;
        _resumenEmitido = true;
        _sb.Length = 0;
        _sb.Append("[TRACK] RESUMEN pérdidas por tramo de pitch (° hacia abajo): ");
        for (int i = 0; i < TRAMOS; i++)
            _sb.Append(i * 10).Append(i == TRAMOS - 1 ? "+" : "-" + ((i + 1) * 10)).Append("°=").Append(ResumenTramo(i)).Append(i < TRAMOS - 1 ? " | " : "");
        Debug.Log(_sb.ToString(), this);
    }

    private string ResumenTramo(int i) => _perdidasPorTramo[i] + " (" + _segPerdidosPorTramo[i].ToString("0.0") + " s)";
}
