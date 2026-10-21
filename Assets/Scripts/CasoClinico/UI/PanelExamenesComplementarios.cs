using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel 6 — Exámenes Complementarios. Anclado a un slot fijo de la sala (SlotPanel; <see cref="raiz"/> es hija del slot),
/// mismo patrón de activación que los demás paneles del módulo. Estética teal diegética.
///
/// La lista de exámenes es DINÁMICA y depende del submodo: se instancia un botón
/// (<see cref="prefabBotonExamen"/>) por examen dentro de <see cref="contenedorExamenes"/>
/// (con VerticalLayoutGroup).
///   - TUTORIAL: los exámenes pertinentes del caso (lista corta, como siempre).
///   - EVALUACIÓN: TODO el <see cref="CatalogoExamenes"/>, en el orden del catálogo (igual
///     para todos los casos, nada resaltado), con una pestaña por categoría — así el
///     jugador puede equivocarse. El resultado sale de la ficha si el caso trae el examen,
///     si no del resultado normal del catálogo.
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
    [Header("Visual (raíz hija de un SlotPanel)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio (IPanelOcultable).
    private bool _visibleSegunEstado;

    [Header("Lista de exámenes (generada por caso)")]
    [SerializeField] private Transform contenedorExamenes;
    [Tooltip("Template desactivado: Image + BotonMenuFeedback + SeleccionableToque + TMP hijo.")]
    [SerializeField] private GameObject prefabBotonExamen;
    [SerializeField] private Color colorExamenNormal = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorExamenUsado  = new Color(0.03f, 0.10f, 0.11f, 0.70f);

    [Header("Catálogo ampliado (solo modo Evaluación)")]
    [Tooltip("Catálogo completo de exámenes. En Evaluación se muestra TODO, en el orden del catálogo, " +
             "agrupado en pestañas por categoría; en Tutorial se ignora (lista corta del caso).")]
    [SerializeField] private CatalogoExamenes catalogo;
    [Tooltip("Pestañas por categoría (en dos filas, con GridLayoutGroup). Solo visible en Evaluación.")]
    [SerializeField] private Transform contenedorTabs;
    [Tooltip("Template desactivado de pestaña: Image + BotonMenuFeedback + SeleccionableToque + TMP hijo.")]
    [SerializeField] private GameObject prefabTab;
    [SerializeField] private Color colorTabNormal = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorTabActiva = new Color(0.12f, 0.45f, 0.50f, 0.95f);

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
    // Gris desaturado a propósito (2026-09-16), mismo criterio que PanelAnamnesis —
    // ver su comentario para el porqué.
    [SerializeField] private Color colorContinuarBloqueado = new Color(0.32f, 0.32f, 0.32f, 0.65f);
    [Tooltip("Texto de ayuda breve, visible solo mientras el botón Continuar está bloqueado.")]
    [SerializeField] private TMP_Text textoAyudaBloqueado;
    [SerializeField] private string mensajeAyudaBloqueado = "Solicita al menos un examen para continuar.";

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;

    private readonly StringBuilder _resultados = new StringBuilder(512);
    private readonly HashSet<string> _revelados = new HashSet<string>();
    private readonly List<GameObject> _botones = new List<GameObject>();
    private readonly List<string> _categoriaDeBoton = new List<string>(); // paralela a _botones (null en Tutorial)
    private readonly List<GameObject> _tabs = new List<GameObject>();
    private readonly List<string> _categoriasTabs = new List<string>();   // paralela a _tabs
    private string _categoriaActual;
    private BotonMenuFeedback _feedbackContinuar;
    private FichaCaso _casoConstruido;
    private bool _evaluacionConstruida;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        if (prefabBotonExamen != null) prefabBotonExamen.SetActive(false);
        if (prefabTab != null) prefabTab.SetActive(false);
        if (botonContinuar != null) _feedbackContinuar = botonContinuar.GetComponent<BotonMenuFeedback>();
        if (textoAyudaBloqueado != null) textoAyudaBloqueado.text = mensajeAyudaBloqueado;
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
        _categoriaDeBoton.Clear();

        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i] != null)
            {
                _tabs[i].transform.SetParent(null, false);
                Destroy(_tabs[i]);
            }
        _tabs.Clear();
        _categoriasTabs.Clear();
        _categoriaActual = null;
    }

    private void ConstruirBotones()
    {
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;

        // Evaluación = catálogo COMPLETO (para poder equivocarse); Tutorial = solo los
        // exámenes pertinentes del caso (la lista corta de siempre). Sin catálogo
        // asignado se cae al comportamiento de Tutorial.
        bool evaluacion = gestorCaso != null && gestorCaso.SubmodoActual == SubmodoCaso.Evaluacion && catalogo != null;

        // Reconstruir solo si cambió el caso o el modo (o aún no hay botones). Retroceder desde
        // RegistroDiagnostico y volver a entrar conserva los botones y su estado "usado".
        if (caso == _casoConstruido && evaluacion == _evaluacionConstruida && _botones.Count > 0) return;

        DestruirBotones();
        _casoConstruido = caso;
        _evaluacionConstruida = evaluacion;

        if (contenedorTabs != null) contenedorTabs.gameObject.SetActive(evaluacion);

        if (caso == null || contenedorExamenes == null || prefabBotonExamen == null) return;

        if (evaluacion)
        {
            // Mismo orden para TODOS los casos (el del catálogo): nada resaltado y los
            // exámenes del caso no van primero — la presentación no delata cuáles son.
            foreach (var entrada in catalogo.entradas)
                if (entrada != null && !string.IsNullOrEmpty(entrada.nombreExamen))
                    CrearBoton(entrada.nombreExamen, entrada.categoria);

            ConstruirTabs();
            SeleccionarCategoria(_categoriasTabs.Count > 0 ? _categoriasTabs[0] : null);
        }
        else if (caso.examenesDisponibles != null)
        {
            foreach (var examen in caso.examenesDisponibles)
                if (examen != null && !string.IsNullOrEmpty(examen.nombreExamen) && examen.EsPertinente)
                    CrearBoton(examen.nombreExamen, null);
        }
    }

    private void CrearBoton(string nombre, string categoria)
    {
        var go = Instantiate(prefabBotonExamen, contenedorExamenes);
        go.SetActive(true);

        var texto = go.GetComponentInChildren<TMP_Text>();
        if (texto != null) texto.text = nombre;

        var goCap = go;
        var toque = go.GetComponentInChildren<SeleccionableToque>();
        if (toque != null) toque.OnSeleccionado += () => SolicitarExamen(nombre, goCap);

        if (_revelados.Contains(nombre)) MarcarUsado(go);
        _botones.Add(go);
        _categoriaDeBoton.Add(categoria);
    }

    // ── Pestañas por categoría (solo Evaluación) ─────────────────────────────

    private void ConstruirTabs()
    {
        if (contenedorTabs == null || prefabTab == null) return;

        catalogo.ObtenerCategorias(_categoriasTabs);
        foreach (var categoria in _categoriasTabs)
        {
            var tab = Instantiate(prefabTab, contenedorTabs);
            tab.SetActive(true);

            var texto = tab.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = categoria.ToUpperInvariant();

            var cat = categoria;
            var toque = tab.GetComponentInChildren<SeleccionableToque>();
            if (toque != null) toque.OnSeleccionado += () => SeleccionarCategoria(cat);
            _tabs.Add(tab);
        }
    }

    private void SeleccionarCategoria(string categoria)
    {
        _categoriaActual = categoria;

        // Solo los botones de la categoría activa están activos: los demás se desregistran
        // del árbitro de toque (no hay botones "fantasma" tocables fuera de la vista).
        for (int i = 0; i < _botones.Count; i++)
            if (_botones[i] != null) _botones[i].SetActive(_categoriaDeBoton[i] == categoria);

        for (int i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i] == null) continue;
            bool activa = _categoriasTabs[i] == categoria;
            // Apaga el BotonMenuFeedback de la pestaña activa para que su lerp no pise
            // el color de "seleccionada" (mismo truco que el estado "usado").
            var fb = _tabs[i].GetComponent<BotonMenuFeedback>();
            if (fb != null) fb.enabled = !activa;
            var img = _tabs[i].GetComponent<Image>();
            if (img != null && activa) img.color = colorTabActiva;
            else if (img != null)      img.color = colorTabNormal;
        }
    }

    // Resultado del examen en ESTE caso: el de la ficha si lo trae, si no el normal del
    // catálogo (ver CatalogoExamenes.ResolverResultado).
    private string ResolverResultado(string nombre)
    {
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;
        string r = catalogo != null ? catalogo.ResolverResultado(caso, nombre) : null;
        if (string.IsNullOrEmpty(r) && caso != null && caso.examenesDisponibles != null)
            foreach (var e in caso.examenesDisponibles)
                if (e != null && e.nombreExamen == nombre) { r = e.resultado; break; }
        return r;
    }

    private void SolicitarExamen(string nombre, GameObject boton)
    {
        // Siempre se registra en el Motor (HashSet.Add idempotente): tocar de nuevo
        // el mismo examen no rompe nada ni suma al conteo de distintos.
        if (interaccionPaciente != null) interaccionPaciente.SolicitarExamen(nombre);

        // Dedup visual: si ya estaba revelado, no se agrega otra entrada.
        if (!_revelados.Add(nombre)) return;

        string resultado = ResolverResultado(nombre);
        if (_resultados.Length > 0) _resultados.Append("\n\n");
        _resultados.Append("<b>").Append(nombre).Append("</b>\n")
                   .Append(string.IsNullOrEmpty(resultado) ? "(sin resultado registrado)" : resultado);
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
        if (textoAyudaBloqueado != null) textoAyudaBloqueado.gameObject.SetActive(!habilitado);
    }

    private void Continuar()
    {
        if (gestorCaso != null && gestorCaso.ExamenesSolicitadosCount >= 1)
            gestorCaso.AvanzarFase(); // ExamenesComplementarios → RegistroDiagnostico
    }
}
