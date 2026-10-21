using TMPro;
using UnityEngine;

/// <summary>
/// Cuenta regresiva visible de la sesión de RCP ("1:45"). Antes de la primera compresión
/// muestra la duración completa ("2:00") con "Empieza a comprimir". Solo se ve en modo
/// Simulación y mientras la sesión no ha terminado; se oculta con el menú de pausa
/// (<see cref="IPanelOcultable"/>, suscripción directa a <see cref="GestorMenuPausa"/>: en
/// SceneSimulador no hay PuenteVisibilidadPausa). Cero asignaciones por fotograma: el texto
/// se reescribe solo cuando cambia el segundo mostrado, con un buffer de char reutilizado.
/// </summary>
public class CronometroSesionRCP : MonoBehaviour, IPanelOcultable
{
    [SerializeField] private SesionRCP sesion;
    [SerializeField] private LogicaRCP logica;
    [SerializeField] private GestorMenuPausa menuPausa;
    [Tooltip("Hijo que se activa/desactiva (el controlador queda siempre activo).")]
    [SerializeField] private GameObject raiz;
    [SerializeField] private TMP_Text textoTiempo;
    [SerializeField] private TMP_Text textoAyuda;

    private readonly char[] _buf = new char[6];
    private int _segMostrados = -1;
    private bool _ocultoPorPausa;
    private bool _visible;
    private EstadoSesionRCP _estadoMostrado = EstadoSesionRCP.Terminada; // fuerza el primer refresco

    void OnEnable()
    {
        if (sesion != null) sesion.AlCambiarEstado += AlCambiarEstadoSesion;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += OcultarTemporalmente;
            menuPausa.AlCerrarMenu += RestaurarVisibilidad;
        }
        _segMostrados = -1;
        AplicarVisibilidad(true);
    }

    void OnDisable()
    {
        if (sesion != null) sesion.AlCambiarEstado -= AlCambiarEstadoSesion;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= OcultarTemporalmente;
            menuPausa.AlCerrarMenu -= RestaurarVisibilidad;
        }
    }

    private void AlCambiarEstadoSesion(EstadoSesionRCP estado) => AplicarVisibilidad(false);

    void Update()
    {
        if (sesion == null) return;
        AplicarVisibilidad(false);
        if (!_visible) return;

        var estado = sesion.Estado;
        if (estado != _estadoMostrado)
        {
            _estadoMostrado = estado;
            if (textoAyuda != null)
                textoAyuda.gameObject.SetActive(estado == EstadoSesionRCP.EsperandoPrimeraCompresion);
            _segMostrados = -1;
        }

        int seg = estado == EstadoSesionRCP.EsperandoPrimeraCompresion
            ? Mathf.CeilToInt(sesion.DuracionSesionSeg)
            : Mathf.CeilToInt(sesion.SegundosRestantes);
        if (seg != _segMostrados)
        {
            _segMostrados = seg;
            EscribirTiempo(seg);
        }
    }

    private void AplicarVisibilidad(bool forzar)
    {
        bool debeVerse = sesion != null && logica != null
                         && !logica.modoCalibracion
                         && sesion.Estado != EstadoSesionRCP.Terminada
                         && !_ocultoPorPausa;
        if (!forzar && debeVerse == _visible) return;
        _visible = debeVerse;
        if (raiz != null) raiz.SetActive(debeVerse);
        if (debeVerse) { _segMostrados = -1; _estadoMostrado = EstadoSesionRCP.Terminada; }
    }

    // "m:ss" (o "mm:ss") sin crear strings: se escribe en un char[] y se entrega a TMP.
    private void EscribirTiempo(int segundosTotales)
    {
        if (textoTiempo == null) return;
        int m = segundosTotales / 60, s = segundosTotales % 60, n = 0;
        if (m >= 10) _buf[n++] = (char)('0' + (m / 10) % 10);
        _buf[n++] = (char)('0' + m % 10);
        _buf[n++] = ':';
        _buf[n++] = (char)('0' + s / 10);
        _buf[n++] = (char)('0' + s % 10);
        textoTiempo.SetCharArray(_buf, 0, n);
    }

    public void OcultarTemporalmente()
    {
        _ocultoPorPausa = true;
        AplicarVisibilidad(false);
    }

    public void RestaurarVisibilidad()
    {
        _ocultoPorPausa = false;
        AplicarVisibilidad(false);
    }
}
