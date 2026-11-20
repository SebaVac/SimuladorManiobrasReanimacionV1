using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel 9 — Debriefing. Anclado a un slot fijo de la sala (SlotPanel; <see cref="raiz"/> es hija del slot),
/// estética teal diegética consistente con el resto del módulo (a diferencia
/// del Panel 7, que rompe estilo a propósito).
///
/// Único momento en que se revela el desempeño: se suscribe a
/// <see cref="GestorCasoClinico.OnCasoFinalizado"/> y muestra el
/// <see cref="ResultadoCaso"/> completo (puntaje, diagnóstico, acciones
/// críticas cumplidas/omitidas, errores frecuentes con su retroalimentación).
/// No agrega ningún accessor nuevo — todo el dato ya era público en
/// ResultadoCaso/AccionCritica/ErrorFrecuente.
///
/// Jerarquía visual: el DIAGNÓSTICO correcto/incorrecto es el titular (grande,
/// arriba). Debajo:
///   - EVALUACIÓN: puntaje total prominente (X / 100) + desglose por componente
///     (Diagnóstico, Manejo, Exámenes, Anamnesis, Examen físico, Errores).
///   - TUTORIAL: el mismo desglose pero SIN total ni nota — "Modo Tutorial: sin
///     puntaje" (el modo Tutorial no puntúa; ver <see cref="ResultadoCaso.EsEvaluacion"/>).
/// El cálculo vive entero en MotorEvaluacion/ConfigPuntaje; este panel solo presenta.
///
/// NO implementa IPanelOcultable: confirmado que el menú de pausa nunca está
/// disponible en EstadoCasoClinico.Debriefing (<see cref="PuenteVisibilidadPausa"/>
/// solo habilita la pausa en ExamenFisico/Anamnesis/ExamenesComplementarios/
/// RegistroDiagnostico), así que ese código sería inalcanzable.
///
/// Visible únicamente en <see cref="EstadoCasoClinico.Debriefing"/>.
///
/// PROTOCOLO DE ESTUDIO: con <see cref="unaSolaPasada"/> (true) tras el Debriefing solo queda «Finalizar», que vuelve a
/// SceneInicio; no aparecen «Reintentar este caso» ni «Volver a selección de caso». En false los botones son los de siempre.
/// Además muestra el código del participante y una línea discreta «Resultados guardados» / «No se pudieron guardar»
/// (estado que informa <see cref="RegistroCasoClinico"/>).
/// </summary>
public class PanelDebriefing : MonoBehaviour
{
    [Header("Visual (raíz hija de un SlotPanel)")]
    [SerializeField] private GameObject raiz;

    [Header("Resumen")]
    [SerializeField] private TMP_Text textoPuntaje;
    [SerializeField] private TMP_Text textoDiagnostico;
    [Tooltip("Línea discreta al pie: 'Dispositivo de entrada: Manos / Controles / Mixto'. No afecta al puntaje.")]
    [SerializeField] private TMP_Text textoDispositivo;
    [Tooltip("Columna izquierda del desglose (nombres de componente), una línea por componente.")]
    [SerializeField] private TMP_Text textoDesgloseNombres;
    [Tooltip("Columna derecha del desglose (obtenidos / máximo), alineada fila por fila con los nombres.")]
    [SerializeField] private TMP_Text textoDesgloseValores;

    [Header("Detalle (acumulativo, scroll)")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text textoResumen;
    [SerializeField] private SeleccionableToque botonScrollArriba;
    [SerializeField] private SeleccionableToque botonScrollAbajo;
    [Range(0.05f, 0.6f)]
    [SerializeField] private float pasoScroll = 0.25f;

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonReintentar;
    [SerializeField] private SeleccionableToque botonVolver;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;

    [Header("Protocolo de estudio")]
    [Tooltip("true: tras el Debriefing solo aparece «Finalizar» (vuelve a SceneInicio); sin «Reintentar este caso» ni «Volver a selección de caso». " +
             "false: comportamiento de siempre.")]
    [SerializeField] private bool unaSolaPasada = true;
    [Tooltip("Registro del estudio: informa si la fila del caso se guardó. Vacío = no se muestra la línea de guardado.")]
    [SerializeField] private RegistroCasoClinico registro;
    [Tooltip("Línea discreta «Resultados guardados» / «No se pudieron guardar».")]
    [SerializeField] private TMP_Text textoGuardado;

    private ResultadoCaso _resultado;
    private Vector2 _posVolverOriginal;
    private string _etiquetaVolverOriginal;
    private bool _modoLeido;
    private static readonly Color ColorGuardadoOk = new Color(0.62f, 0.74f, 0.78f, 1f);
    private static readonly Color ColorGuardadoError = new Color(0.95f, 0.85f, 0.15f, 1f);

    public bool UnaSolaPasada => unaSolaPasada;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    void OnEnable()
    {
        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado  += AlCambioEstado;
            gestorCaso.OnCasoFinalizado += AlCasoFinalizado;
        }
        if (botonReintentar != null) botonReintentar.OnSeleccionado += Reintentar;
        if (botonVolver != null)     botonVolver.OnSeleccionado     += Volver;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado += ScrollArriba;
        if (botonScrollAbajo != null)  botonScrollAbajo.OnSeleccionado  += ScrollAbajo;
        if (registro != null) registro.AlGuardar += AlGuardarResultado;
        AplicarModoPasada();
    }

    void OnDisable()
    {
        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado  -= AlCambioEstado;
            gestorCaso.OnCasoFinalizado -= AlCasoFinalizado;
        }
        if (botonReintentar != null) botonReintentar.OnSeleccionado -= Reintentar;
        if (botonVolver != null)     botonVolver.OnSeleccionado     -= Volver;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado -= ScrollArriba;
        if (botonScrollAbajo != null)  botonScrollAbajo.OnSeleccionado  -= ScrollAbajo;
        if (registro != null) registro.AlGuardar -= AlGuardarResultado;
    }

    // «Una sola pasada»: solo «Finalizar» (el botón Volver, recentrado y renombrado); sin Reintentar.
    private void AplicarModoPasada()
    {
        if (botonVolver == null) return;
        var etiqueta = botonVolver.GetComponentInChildren<TMP_Text>(true);
        var rt = (RectTransform)botonVolver.transform;
        if (!_modoLeido)
        {
            _posVolverOriginal = rt.anchoredPosition;
            _etiquetaVolverOriginal = etiqueta != null ? etiqueta.text : null;
            _modoLeido = true;
        }
        if (botonReintentar != null) botonReintentar.gameObject.SetActive(!unaSolaPasada);
        rt.anchoredPosition = unaSolaPasada ? new Vector2(0f, _posVolverOriginal.y) : _posVolverOriginal;
        if (etiqueta != null && _etiquetaVolverOriginal != null) etiqueta.text = unaSolaPasada ? "FINALIZAR" : _etiquetaVolverOriginal;
    }

    private void AlGuardarResultado(bool ok, string estado) { ActualizarGuardado(); }

    // Línea de estado del guardado: solo cuando ya se intentó escribir la fila de ESTE caso.
    private void ActualizarGuardado()
    {
        if (textoGuardado == null) return;
        bool ver = registro != null && registro.GuardarResultados && registro.GuardadoEsteCaso;
        textoGuardado.gameObject.SetActive(ver);
        if (!ver) return;
        textoGuardado.text = registro.UltimoGuardadoOk ? "Resultados guardados" : "No se pudieron guardar";
        textoGuardado.color = registro.UltimoGuardadoOk ? ColorGuardadoOk : ColorGuardadoError;
    }

    // ── Visibilidad + contenido ──────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        bool visible = estado == EstadoCasoClinico.Debriefing;
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
    }

    // Llega DESPUÉS de AlCambioEstado(Debriefing) en el mismo frame (ver
    // GestorCasoClinico.OnJugadorRegistroDiagnostico: Transicionar antes de
    // invocar OnCasoFinalizado) — para cuando se renderiza, ambos ya corrieron.
    private void AlCasoFinalizado(ResultadoCaso resultado)
    {
        _resultado = resultado;
        Poblar();
    }

    private void Poblar()
    {
        if (_resultado == null) return;

        // Titular del resumen: el diagnóstico, no el puntaje (ver comentario de
        // clase). Sin glifo ✓/✕ a propósito — mismo criterio que Panel 5
        // (LiberationSans SDF no garantiza esos glifos en su tabla primaria);
        // color + texto ya distinguen el estado sin depender de eso.
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;
        if (textoDiagnostico != null)
        {
            if (_resultado.diagnosticoCorrecto)
            {
                textoDiagnostico.text = "<color=#8FE0A0>DIAGNÓSTICO CORRECTO</color>";
            }
            else
            {
                string esperado = caso != null ? caso.diagnosticoEsperado : null;
                textoDiagnostico.text = "<color=#E08F8F>DIAGNÓSTICO INCORRECTO</color>" +
                    (string.IsNullOrEmpty(esperado) ? "" : $"\n<size=65%>Esperado: {esperado}</size>");
            }
        }

        // Puntaje total solo en Evaluación; Tutorial no puntúa (sin total ni "nota").
        if (textoPuntaje != null)
            textoPuntaje.text = _resultado.EsEvaluacion
                ? $"PUNTAJE: {_resultado.puntajeTotal} / {_resultado.puntajeMaximo}"
                : "<size=65%><color=#8FA3A8>Modo Tutorial: sin puntaje</color></size>";

        PoblarDesglose();

        if (textoDispositivo != null)
        {
            string disp = _resultado.dispositivoEntrada == DispositivoEntrada.Controles ? "Controles"
                        : _resultado.dispositivoEntrada == DispositivoEntrada.Mixto ? "Mixto" : "Manos";
            textoDispositivo.text = "Dispositivo de entrada: " + disp
                + (CodigoParticipante.HayCodigo ? "  ·  Código: " + CodigoParticipante.Actual : "");
        }
        ActualizarGuardado();

        var sb = new StringBuilder(512);
        sb.Append("<b>ACCIONES CRÍTICAS</b>\n");
        foreach (var accion in _resultado.accionesCumplidas)
            sb.Append("<color=#8FE0A0>CUMPLIDA</color> — ").Append(accion.descripcion).Append('\n');
        foreach (var accion in _resultado.accionesOmitidas)
            sb.Append("<color=#E08F8F>OMITIDA</color> — ").Append(accion.descripcion).Append('\n');

        if (_resultado.erroresDetectados.Count > 0)
        {
            sb.Append("\n<b>ERRORES FRECUENTES DETECTADOS</b>\n");
            foreach (var error in _resultado.erroresDetectados)
                sb.Append("• ").Append(error.descripcion).Append('\n')
                  .Append("  ").Append(error.retroalimentacion).Append('\n');
        }

        if (textoResumen != null) textoResumen.text = sb.ToString();
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f; // arranca mostrando el principio
        }
    }

    // Desglose por componente en dos columnas alineadas (nombres | obtenidos / máximo).
    // Un componente que no aplica al caso (máximo 0) se muestra como "—".
    private void PoblarDesglose()
    {
        if (textoDesgloseNombres == null || textoDesgloseValores == null) return;

        var nombres = new StringBuilder(128);
        var valores = new StringBuilder(128);

        void Fila(string nombre, ComponentePuntaje c)
        {
            nombres.Append(nombre).Append('\n');
            if (c == null || c.maximo <= 0f) valores.Append("—\n");
            else valores.Append(Formatear(c.obtenido)).Append(" / ").Append(Formatear(c.maximo)).Append('\n');
        }

        Fila("Diagnóstico",   _resultado.diagnostico);
        Fila("Manejo",        _resultado.manejo);
        Fila("Exámenes",      _resultado.examenes);
        Fila("Anamnesis",     _resultado.anamnesis);
        Fila("Examen físico", _resultado.examenFisico);

        nombres.Append("Errores frecuentes");
        valores.Append(_resultado.penalizacionErrores > 0f
            ? "<color=#E08F8F>-" + Formatear(_resultado.penalizacionErrores) + "</color>"
            : "0");

        textoDesgloseNombres.text = nombres.ToString();
        textoDesgloseValores.text = valores.ToString();
    }

    // Hasta 1 decimal, sin ceros sobrantes ("12.5", "20"); cultura invariante.
    private static string Formatear(float v) =>
        v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    // ── Scroll ───────────────────────────────────────────────────────────────

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

    // ── Botones ──────────────────────────────────────────────────────────────

    private void Reintentar()
    {
        if (gestorCaso != null) gestorCaso.ReiniciarCasoActual();
    }

    // Vuelve a PanelSeleccionSubmodo (Panel 2) DENTRO del módulo, no a
    // SceneInicio (2026-09-23) — reutiliza AbandonarCaso() sin variante nueva:
    // ya hace exactamente lo necesario (detiene detección + transiciona a
    // Inactivo, que es el estado en el que PanelSeleccionSubmodo se muestra
    // solo) y no tiene ninguna lógica atada a sus otros dos llamadores
    // (RetrocederFase desde Introduccion, BarraNavegacionCaso.Salir) que sea
    // incompatible con invocarlo desde Debriefing. Salir del módulo por
    // completo sigue existiendo — vía el BotonVolver propio de
    // PanelSeleccionSubmodo, que sí llama a GestorNavegacionMenu.
    private void Volver()
    {
        if (gestorCaso == null) { Debug.LogError("[PanelDebriefing] Falta GestorCasoClinico.", this); return; }

        if (unaSolaPasada)
        {
            // «Finalizar»: cierra el caso (estado Finalizado: ya no cuenta como abandonado) y vuelve a SceneInicio por la misma
            // ruta que «Salir al menú principal» del menú de pausa (GestorMenuPausa.VolverAlMenuPrincipal).
            gestorCaso.FinalizarCaso();
            UnityEngine.SceneManagement.SceneManager.LoadScene(GestorNavegacionMenu.escenaMenuPrincipal);
            return;
        }
        gestorCaso.AbandonarCaso();
    }
}
