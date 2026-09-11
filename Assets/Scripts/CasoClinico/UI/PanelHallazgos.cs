using TMPro;
using UnityEngine;

/// <summary>
/// Panel 4 — Hallazgos / Examen Físico. Head-locked (AnclajeCabeza en <see cref="raiz"/>),
/// mismo patrón de activación que PanelIntroduccion / PanelSeleccionSubmodo / Panel 7:
/// el controlador queda siempre activo y alterna <see cref="raiz"/> según el estado.
///
/// Estética teal diegética, consistente con el resto del módulo (a diferencia del Panel 7).
///
/// Interacción: el jugador acerca la mano a una región del paciente; `InteraccionPaciente`
/// dispara <see cref="InteraccionPaciente.OnRegionExaminada"/> y este panel muestra el
/// `hallazgoTexto` de esa región. Evaluación invisible: NO muestra si el hallazgo era
/// relevante (`esHallazgoRelevante` es solo para MotorEvaluacion).
///
/// Cada región dispara su evento una sola vez por caso (dedupe en InteraccionPaciente):
/// el panel muestra el hallazgo de la última región examinada, sin historial.
///
/// Visible únicamente en <see cref="EstadoCasoClinico.ExamenFisico"/>.
/// </summary>
public class PanelHallazgos : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio. Nota: el paciente
    // y los marcadores de región NO se ocultan con el menú de pausa (no son parte
    // del "panel" que se solapaba con el menú; siguen atados solo al estado del caso).
    private bool _visibleSegunEstado;

    [Header("Contenido")]
    [SerializeField] private TMP_Text textoNombreRegion;
    [SerializeField] private TMP_Text textoHallazgo;
    [Tooltip("Mensaje mientras el jugador aún no ha examinado ninguna región.")]
    [SerializeField] private string textoInicial = "Acerca la mano a una región del paciente para examinarla.";
    [Tooltip("Fallback si la región examinada no está en casoActual.hallazgosPorRegion (no debería ocurrir).")]
    [SerializeField] private string textoSinHallazgo = "Sin hallazgos registrados para esta región.";

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonContinuar;

    [Header("Marcadores de región")]
    [Tooltip("Raíz de los Transform de región; sus MeshRenderer (marcadores) se muestran solo en ExamenFisico.")]
    [SerializeField] private Transform regionesRoot;

    [Header("Paciente")]
    [Tooltip("Raíz del paciente (Paciente_Completo). Sus SkinnedMeshRenderer se muestran " +
             "SOLO en ExamenFisico — fuera de esa fase el cuerpo supino chocaría con los " +
             "paneles head-locked. Cuando existan los paneles de Anamnesis / Exámenes " +
             "habrá que revisar si el paciente también debe verse ahí.")]
    [SerializeField] private Transform pacienteRoot;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        SetMarcadoresVisibles(false);
        SetPacienteVisible(false);
    }

    void OnEnable()
    {
        if (gestorCaso != null)        gestorCaso.OnCambioEstado += AlCambioEstado;
        if (interaccionPaciente != null) interaccionPaciente.OnRegionExaminada += AlExaminarRegion;
        if (botonContinuar != null)    botonContinuar.OnSeleccionado += Continuar;
    }

    void OnDisable()
    {
        if (gestorCaso != null)        gestorCaso.OnCambioEstado -= AlCambioEstado;
        if (interaccionPaciente != null) interaccionPaciente.OnRegionExaminada -= AlExaminarRegion;
        if (botonContinuar != null)    botonContinuar.OnSeleccionado -= Continuar;
    }

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        bool visible = estado == EstadoCasoClinico.ExamenFisico;
        _visibleSegunEstado = visible;
        if (visible) Reiniciar();
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
        SetMarcadoresVisibles(visible);
        SetPacienteVisible(visible);
    }

    // ── IPanelOcultable (menú de pausa) ──────────────────────────────────────
    // Solo el panel de texto (raiz); paciente y marcadores siguen atados al estado.

    public void OcultarTemporalmente()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    public void RestaurarVisibilidad()
    {
        if (raiz != null) raiz.SetActive(_visibleSegunEstado);
    }

    private void Reiniciar()
    {
        if (textoNombreRegion != null) textoNombreRegion.text = string.Empty;
        if (textoHallazgo != null)     textoHallazgo.text = textoInicial;
    }

    private void AlExaminarRegion(string region)
    {
        if (gestorCaso == null || gestorCaso.EstadoActual != EstadoCasoClinico.ExamenFisico) return;

        string hallazgo = textoSinHallazgo;
        var caso = gestorCaso.CasoActual;
        if (caso != null && caso.hallazgosPorRegion != null)
        {
            foreach (var h in caso.hallazgosPorRegion)
            {
                if (h == null || h.regionAnatomica != region) continue;
                hallazgo = h.hallazgoTexto;
                break;
            }
        }

        if (textoNombreRegion != null) textoNombreRegion.text = region;
        if (textoHallazgo != null)     textoHallazgo.text = hallazgo;
    }

    private void Continuar()
    {
        if (gestorCaso != null) gestorCaso.AvanzarFase(); // ExamenFisico → Anamnesis
    }

    private void SetMarcadoresVisibles(bool visible)
    {
        if (regionesRoot == null) return;
        var renderers = regionesRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
    }

    private void SetPacienteVisible(bool visible)
    {
        if (pacienteRoot == null) return;
        var renderers = pacienteRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
    }
}
