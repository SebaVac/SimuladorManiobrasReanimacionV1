using UnityEngine;

/// <summary>
/// Primera pantalla de SceneDiagnostico. Panel diegético World Space head-locked
/// a CenterEyeAnchor (vía <see cref="AnclajeCabeza"/> en <see cref="raiz"/>),
/// mismo tratamiento que el Panel de Introducción.
///
/// Tres tarjetas (SeleccionableToque + BotonMenuFeedback) fijan el SubmodoCaso y
/// disparan la selección aleatoria de caso; un botón "Volver" regresa a SceneInicio.
///
/// Controlador siempre activo; alterna la visibilidad de <see cref="raiz"/> según
/// el estado del caso (visible solo mientras EstadoActual == Inactivo).
/// </summary>
public class PanelSeleccionSubmodo : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio.
    private bool _visibleSegunEstado;

    [Header("Tarjetas (SeleccionableToque)")]
    [SerializeField] private SeleccionableToque tarjetaTutorial;
    [SerializeField] private SeleccionableToque tarjetaGuiado;
    [SerializeField] private SeleccionableToque tarjetaEvaluacion;
    [SerializeField] private SeleccionableToque botonVolver;

    [Header("Sistema")]
    [SerializeField] private SelectorCasoAleatorio selectorCaso;
    [SerializeField] private GestorNavegacionMenu navegacion;
    [SerializeField] private GestorCasoClinico gestorCaso;

    void Start()
    {
        AplicarVisibilidad(gestorCaso != null ? gestorCaso.EstadoActual : EstadoCasoClinico.Inactivo);
    }

    void OnEnable()
    {
        Suscribir(tarjetaTutorial,   ElegirTutorial,   true);
        Suscribir(tarjetaGuiado,     ElegirGuiado,     true);
        Suscribir(tarjetaEvaluacion, ElegirEvaluacion, true);
        Suscribir(botonVolver,       Volver,           true);
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AplicarVisibilidad;
    }

    void OnDisable()
    {
        Suscribir(tarjetaTutorial,   ElegirTutorial,   false);
        Suscribir(tarjetaGuiado,     ElegirGuiado,     false);
        Suscribir(tarjetaEvaluacion, ElegirEvaluacion, false);
        Suscribir(botonVolver,       Volver,           false);
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AplicarVisibilidad;
    }

    private static void Suscribir(SeleccionableToque s, System.Action h, bool add)
    {
        if (s == null) return;
        if (add) s.OnSeleccionado += h;
        else     s.OnSeleccionado -= h;
    }

    // ── Selección de submodo ────────────────────────────────────────────────

    private void ElegirTutorial()   => Elegir(SubmodoCaso.Tutorial);
    private void ElegirGuiado()     => Elegir(SubmodoCaso.Guiado);
    private void ElegirEvaluacion() => Elegir(SubmodoCaso.Evaluacion);

    private void Elegir(SubmodoCaso submodo)
    {
        if (selectorCaso == null)
        {
            Debug.LogError("[PanelSeleccionSubmodo] Falta SelectorCasoAleatorio.", this);
            return;
        }
        // Dispara IniciarCaso → OnCambioEstado(Introduccion) → este panel se oculta.
        selectorCaso.SeleccionarYComenzar(submodo);
    }

    private void Volver()
    {
        if (navegacion != null) navegacion.VolverAlMenuPrincipal();
        else Debug.LogError("[PanelSeleccionSubmodo] Falta GestorNavegacionMenu.", this);
    }

    // ── Visibilidad ────────────────────────────────────────────────────────

    private void AplicarVisibilidad(EstadoCasoClinico estado)
    {
        bool visible = estado == EstadoCasoClinico.Inactivo;
        _visibleSegunEstado = visible;
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
    }

    // ── IPanelOcultable (menú de pausa) ──────────────────────────────────────

    public void OcultarTemporalmente()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    public void RestaurarVisibilidad()
    {
        if (raiz != null) raiz.SetActive(_visibleSegunEstado);
    }
}
