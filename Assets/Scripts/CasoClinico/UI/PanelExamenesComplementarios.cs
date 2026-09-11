using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel 6 — Exámenes Complementarios. Head-locked (AnclajeCabeza en <see cref="raiz"/>),
/// mismo patrón de activación que los demás paneles del módulo. Estética teal diegética.
///
/// La lista de exámenes es DINÁMICA por caso: se instancia un botón
/// (<see cref="prefabBotonExamen"/>) por cada <c>ExamenComplementario</c> de
/// <c>casoActual.examenesDisponibles</c> (entre 3 y 4 según el caso) dentro de
/// <see cref="contenedorExamenes"/> (con VerticalLayoutGroup).
///
/// Al tocar un examen: se llama <see cref="InteraccionPaciente.SolicitarExamen"/> con
/// su <c>nombreExamen</c> (identificador que espera MotorEvaluacion / SeDetectaError,
/// prefijo <c>solicito:</c>), y se agrega su <c>resultado</c> al área acumulativa
/// scrolleable. Volver a tocar un examen ya solicitado: se re-llama SolicitarExamen
/// (idempotente en el Motor) pero NO se agrega otra entrada visual — el resultado ya
/// está a la vista y el botón queda atenuado.
///
/// "Continuar a Registro de Diagnóstico" arranca bloqueado y se habilita cuando
/// <see cref="GestorCasoClinico.ExamenesSolicitadosCount"/> ≥ 1 (exámenes DISTINTOS;
/// repetir no cuenta). Mismo patrón visual que el botón de Anamnesis.
///
/// Visible únicamente en <see cref="EstadoCasoClinico.ExamenesComplementarios"/>.
/// Los resultados se limpian al arrancar un caso nuevo (Introduccion), NO al
/// re-entrar (retroceder desde RegistroDiagnostico conserva lo solicitado).
/// </summary>
public class PanelExamenesComplementarios : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio (IPanelOcultable).
    private bool _visibleSegunEstado;

    [Header("Lista de exámenes (generada por caso)")]
    [SerializeField] private Transform contenedorExamenes;
    [Tooltip("Template desactivado: Image + BotonMenuFeedback + SeleccionableToque + TMP hijo.")]
    [SerializeField] private GameObject prefabBotonExamen;
    [SerializeField] private Color colorExamenNormal = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorExamenUsado  = new Color(0.03f, 0.10f, 0.11f, 0.70f);

    [Header("Resultados (acumulativos, scroll)")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text textoResultados;
    [SerializeField] private string textoInicial = "Toca un examen de la lista para solicitarlo.";
    [SerializeField] private SeleccionableToque botonScrollArriba;
    [SerializeField] private SeleccionableToque botonScrollAbajo;
    [Range(0.05f, 0.6f)]
    [SerializeField] private float pasoScroll = 0.25f;

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonContinuar;
    [SerializeField] private Image fondoBotonContinuar;
    [SerializeField] private Color colorContinuarActivo    = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorContinuarBloqueado = new Color(0.05f, 0.09f, 0.10f, 0.55f);

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;

    private readonly StringBuilder _resultados = new StringBuilder(512);
    private readonly HashSet<string> _revelados = new HashSet<string>();
    private readonly List<GameObject> _botones = new List<GameObject>();
    private BotonMenuFeedback _feedbackContinuar;
    private FichaCaso _casoConstruido;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        if (prefabBotonExamen != null) prefabBotonExamen.SetActive(false);
        if (botonContinuar != null) _feedbackContinuar = botonContinuar.GetComponent<BotonMenuFeedback>();
        Reiniciar();
    }

    void OnEnable()
    {
        if (gestorCaso != null)        gestorCaso.OnCambioEstado       += AlCambioEstado;
        if (botonContinuar != null)    botonContinuar.OnSeleccionado   += Continuar;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado += ScrollArriba;
        if (botonScrollAbajo != null)  botonScrollAbajo.OnSeleccionado  += ScrollAbajo;
    }

    void OnDisable()
    {
        if (gestorCaso != null)        gestorCaso.OnCambioEstado       -= AlCambioEstado;
        if (botonContinuar != null)    botonContinuar.OnSeleccionado   -= Continuar;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado -= ScrollArriba;
        if (botonScrollAbajo != null)  botonScrollAbajo.OnSeleccionado  -= ScrollAbajo;
    }

    // ── Visibilidad ──────────────────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        if (estado == EstadoCasoClinico.Introduccion) Reiniciar();

        bool visible = estado == EstadoCasoClinico.ExamenesComplementarios;
        _visibleSegunEstado = visible;
        if (visible) ConstruirBotones();
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
        if (visible) ActualizarBotonContinuar();
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

    private void Reiniciar()
    {
        _resultados.Clear();
        _revelados.Clear();
        DestruirBotones();
        _casoConstruido = null;
        if (textoResultados != null) textoResultados.text = textoInicial;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        ActualizarBotonContinuar();
    }

    // ── Lista dinámica de exámenes ───────────────────────────────────────────

    private void DestruirBotones()
    {
        for (int i = 0; i < _botones.Count; i++)
            if (_botones[i] != null)
            {
                // Desanidar YA (Destroy es diferido a fin de frame) para que el
                // contenedor no cuente botones fantasma si se reconstruye.
                _botones[i].transform.SetParent(null, false);
                Destroy(_botones[i]);
            }
        _botones.Clear();
    }

    private void ConstruirBotones()
    {
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;

        // Reconstruir solo si cambió el caso (o aún no hay botones). Retroceder desde
        // RegistroDiagnostico y volver a entrar conserva los botones y su estado "usado".
        if (caso == _casoConstruido && _botones.Count > 0) return;

        DestruirBotones();
        _casoConstruido = caso;

        if (caso == null || caso.examenesDisponibles == null ||
            contenedorExamenes == null || prefabBotonExamen == null)
            return;

        foreach (var examen in caso.examenesDisponibles)
        {
            if (examen == null || string.IsNullOrEmpty(examen.nombreExamen)) continue;

            var go = Instantiate(prefabBotonExamen, contenedorExamenes);
            go.SetActive(true);

            var texto = go.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = examen.nombreExamen;

            var examenCap = examen;
            var goCap = go;
            var toque = go.GetComponentInChildren<SeleccionableToque>();
            if (toque != null) toque.OnSeleccionado += () => SolicitarExamen(examenCap, goCap);

            if (_revelados.Contains(examen.nombreExamen)) MarcarUsado(go);
            _botones.Add(go);
        }
    }

    private void SolicitarExamen(ExamenComplementario examen, GameObject boton)
    {
        // Siempre se registra en el Motor (HashSet.Add idempotente): tocar de nuevo
        // el mismo examen no rompe nada ni suma al conteo de distintos.
        if (interaccionPaciente != null) interaccionPaciente.SolicitarExamen(examen.nombreExamen);

        // Dedup visual: si ya estaba revelado, no se agrega otra entrada.
        if (!_revelados.Add(examen.nombreExamen)) return;

        if (_resultados.Length > 0) _resultados.Append("\n\n");
        _resultados.Append("<b>").Append(examen.nombreExamen).Append("</b>\n")
                   .Append(string.IsNullOrEmpty(examen.resultado) ? "(sin resultado registrado)" : examen.resultado);
        if (textoResultados != null) textoResultados.text = _resultados.ToString();

        MarcarUsado(boton);
        ActualizarBotonContinuar();
        ScrollAlFinal();
    }

    private void MarcarUsado(GameObject boton)
    {
        if (boton == null) return;
        // Apaga el BotonMenuFeedback para que su lerp no pise el color "usado".
        var fb = boton.GetComponent<BotonMenuFeedback>();
        if (fb != null) fb.enabled = false;
        var img = boton.GetComponent<Image>();
        if (img != null) img.color = colorExamenUsado;
    }

    // ── Scroll de resultados ─────────────────────────────────────────────────

    private void ScrollAlFinal()
    {
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
    }

    private void ScrollArriba()
    {
        if (scroll != null)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + pasoScroll);
    }

    private void ScrollAbajo()
    {
        if (scroll != null)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - pasoScroll);
    }

    // ── Continuar ────────────────────────────────────────────────────────────

    private void ActualizarBotonContinuar()
    {
        bool habilitado = gestorCaso != null && gestorCaso.ExamenesSolicitadosCount >= 1;
        if (botonContinuar != null) botonContinuar.Interactuable = habilitado;

        if (_feedbackContinuar != null) _feedbackContinuar.enabled = habilitado;
        if (fondoBotonContinuar != null)
            fondoBotonContinuar.color = habilitado ? colorContinuarActivo : colorContinuarBloqueado;
    }

    private void Continuar()
    {
        if (gestorCaso != null && gestorCaso.ExamenesSolicitadosCount >= 1)
            gestorCaso.AvanzarFase(); // ExamenesComplementarios → RegistroDiagnostico
    }
}
