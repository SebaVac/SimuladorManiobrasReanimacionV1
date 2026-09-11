using TMPro;
using UnityEngine;

/// <summary>
/// Panel de Introducción del caso. Head-locked a CenterEyeAnchor (vía
/// <see cref="AnclajeCabeza"/> en <see cref="raiz"/>).
///
/// Muestra ÚNICAMENTE <c>motivoConsulta</c> — sin título, sin <c>nombreCaso</c>.
/// El nombre del caso (= diagnóstico) nunca se revela antes del Debriefing.
///
/// Visible solo mientras <see cref="EstadoCasoClinico.Introduccion"/>. El botón
/// "Comenzar" avanza a <see cref="EstadoCasoClinico.ExamenFisico"/>.
/// </summary>
public class PanelIntroduccion : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // true si, según la máquina de estados, este panel debería estar visible ahora
    // mismo. Lo distinto de raiz.activeSelf mientras el menú de pausa lo oculta a
    // la fuerza (ver IPanelOcultable) — RestaurarVisibilidad() vuelve a este valor.
    private bool _visibleSegunEstado;

    [Header("Contenido")]
    [SerializeField] private TMP_Text textoMotivoConsulta;
    [Tooltip("Bloque de texto que explica las 3 fases; solo visible en submodo Tutorial.")]
    [SerializeField] private GameObject bloqueTutorial;

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonComenzar;
    [Tooltip("Vuelve a la selección de submodo (RetrocederFase desde Introduccion).")]
    [SerializeField] private SeleccionableToque botonAtras;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;

    void Awake()
    {
        // Empieza oculto; se muestra al entrar a Introduccion.
        if (raiz != null) raiz.SetActive(false);
    }

    void OnEnable()
    {
        if (botonComenzar != null) botonComenzar.OnSeleccionado += Comenzar;
        if (botonAtras != null)    botonAtras.OnSeleccionado    += Atras;
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
    }

    void OnDisable()
    {
        if (botonComenzar != null) botonComenzar.OnSeleccionado -= Comenzar;
        if (botonAtras != null)    botonAtras.OnSeleccionado    -= Atras;
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
    }

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        bool visible = estado == EstadoCasoClinico.Introduccion;
        _visibleSegunEstado = visible;

        if (visible) Poblar();
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

    private void Poblar()
    {
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;

        if (textoMotivoConsulta != null)
            textoMotivoConsulta.text = caso != null ? caso.motivoConsulta : string.Empty;

        if (bloqueTutorial != null)
        {
            bool esTutorial = gestorCaso != null && gestorCaso.SubmodoActual == SubmodoCaso.Tutorial;
            bloqueTutorial.SetActive(esTutorial);
        }
    }

    private void Comenzar()
    {
        if (gestorCaso == null)
        {
            Debug.LogError("[PanelIntroduccion] Falta GestorCasoClinico.", this);
            return;
        }
        gestorCaso.AvanzarFase(); // Introduccion → ExamenFisico
    }

    private void Atras()
    {
        if (gestorCaso != null) gestorCaso.RetrocederFase(); // Introduccion → Inactivo (selección de submodo)
    }
}
