using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de introducción de SceneDiagnostico (protocolo de estudio). Es lo primero que ve el participante: explica qué hará
/// (atender UN caso: examinar, preguntar, pedir exámenes y registrar diagnóstico e indicaciones) y qué se guarda, pide el
/// código anónimo (<see cref="CodigoParticipante"/>, el mismo de SceneSimulador; campo que abre el teclado nativo con el patrón
/// de InteraccionPaciente) y tiene «Comenzar», gris y deshabilitado con un texto de ayuda mientras el código esté vacío.
///
/// Mismo patrón que los demás paneles del módulo: <see cref="raiz"/> es hija de Slot_Principal, el controlador queda siempre
/// activo, y se oculta/restaura con el menú de pausa (<see cref="IPanelOcultable"/>; PuenteVisibilidadPausa lo descubre solo).
///
///  - <see cref="forzarEvaluacion"/> (true): «Comenzar» salta el panel de selección de submodo e inicia un caso en Evaluación
///    (equivale a tocar la tarjeta «Evaluación»: <see cref="SelectorCasoAleatorio.SeleccionarYComenzar"/>). Si el caso se abandona
///    y la escena vuelve a Inactivo, este panel reaparece (con el código ya cargado) en vez del Panel 2. En false: «Comenzar» solo
///    muestra el Panel 2 como antes.
///  - <see cref="mostrarIntroduccion"/> (true): en false no hay introducción y la escena arranca como antes. (forzarEvaluacion
///    solo actúa a través de este panel.)
/// El Panel 2 (<see cref="PanelSeleccionSubmodo"/>) se mantiene oculto mientras este panel esté pendiente.
/// Sin trabajo por fotograma salvo atender el teclado nativo cuando está abierto.
/// </summary>
public class PanelIntroduccionDiagnostico : MonoBehaviour, IPanelOcultable
{
    [Header("Protocolo")]
    [Tooltip("Muestra esta introducción al cargar la escena. En false la escena arranca como antes (Panel 2 directamente).")]
    [SerializeField] private bool mostrarIntroduccion = true;
    [Tooltip("true: «Comenzar» inicia un caso en modo Evaluación sin pasar por el Panel 2. false: «Comenzar» muestra el Panel 2 como siempre.")]
    [SerializeField] private bool forzarEvaluacion = true;

    [Header("Visual (raíz hija de un SlotPanel)")]
    [SerializeField] private GameObject raiz;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private SelectorCasoAleatorio selectorCaso;
    [SerializeField] private GestorMenuPausa menuPausa;

    [Header("UI")]
    [SerializeField] private TMP_Text textoCuerpo;
    [SerializeField] private TMP_Text textoCodigo;
    [SerializeField] private SeleccionableToque botonCodigo;
    [SerializeField] private SeleccionableToque botonComenzar;
    [SerializeField] private Image fondoBotonComenzar;
    [SerializeField] private TMP_Text textoAyuda;

    [Header("Colores del botón Comenzar")]
    [SerializeField] private Color colorComenzarActivo    = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorComenzarBloqueado = new Color(0.32f, 0.32f, 0.32f, 0.65f);

    [Header("Textos")]
    [SerializeField] private string placeholderCodigo = "Toca aquí para escribir tu código";
    [SerializeField] private string ayudaSinCodigo = "Escribe tu código para poder comenzar.";
    [SerializeField] private string ayudaCodigoInvalido = "Código no válido: usa letras, números o guion.";

    private bool _pendiente;          // la introducción debe mostrarse (cuando el caso esté Inactivo)
    private bool _yaComenzo;          // el participante ya pulsó «Comenzar» al menos una vez
    private bool _codigoRechazado;
    private TouchScreenKeyboard _teclado;
    private BotonMenuFeedback _feedbackComenzar;

    /// <summary>El panel está pendiente: el Panel 2 debe seguir oculto. Cambia al pulsar «Comenzar» y al reaparecer.</summary>
    public bool Pendiente => _pendiente;
    public bool MostrarIntroduccion => mostrarIntroduccion;
    public bool ForzarEvaluacion => forzarEvaluacion;
    public bool ComenzarHabilitado => botonComenzar != null && botonComenzar.Interactuable;
    /// <summary>Cambió <see cref="Pendiente"/>.</summary>
    public event Action AlCambiarPendiente;

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        if (botonComenzar != null) _feedbackComenzar = botonComenzar.GetComponent<BotonMenuFeedback>();
    }

    void OnEnable()
    {
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += OcultarTemporalmente;
            menuPausa.AlCerrarMenu += RestaurarVisibilidad;
        }
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
        if (botonCodigo != null) botonCodigo.OnSeleccionado += AbrirTeclado;
        if (botonComenzar != null) botonComenzar.OnSeleccionado += Comenzar;
    }

    void OnDisable()
    {
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= OcultarTemporalmente;
            menuPausa.AlCerrarMenu -= RestaurarVisibilidad;
        }
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
        if (botonCodigo != null) botonCodigo.OnSeleccionado -= AbrirTeclado;
        if (botonComenzar != null) botonComenzar.OnSeleccionado -= Comenzar;
    }

    void Start()
    {
        _pendiente = mostrarIntroduccion && (gestorCaso == null || gestorCaso.EstadoActual == EstadoCasoClinico.Inactivo);
        if (_pendiente) Refrescar();
        AplicarVisibilidad();
        AlCambiarPendiente?.Invoke();
    }

    void Update()
    {
        if (_teclado != null) ProcesarTeclado(); // solo mientras el teclado nativo está abierto
    }

    // ── Visibilidad ──────────────────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        // Con un caso abandonado (vuelta a Inactivo) en modo forzado, la introducción vuelve a pedir «Comenzar» (otro caso en
        // Evaluación) en lugar de dejar el Panel 2, que permitiría elegir Tutorial.
        if (estado == EstadoCasoClinico.Inactivo && _yaComenzo && forzarEvaluacion && mostrarIntroduccion && !_pendiente)
        {
            _pendiente = true;
            Refrescar();
            AlCambiarPendiente?.Invoke();
        }
        AplicarVisibilidad();
    }

    private bool DebeVerse =>
        _pendiente && (gestorCaso == null || gestorCaso.EstadoActual == EstadoCasoClinico.Inactivo);

    private void AplicarVisibilidad()
    {
        bool v = DebeVerse;
        if (raiz != null && raiz.activeSelf != v) raiz.SetActive(v);
    }

    // IPanelOcultable (menú de pausa): se oculta a la fuerza y al volver recupera lo que su propio estado dicta.
    public void OcultarTemporalmente()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    public void RestaurarVisibilidad() => AplicarVisibilidad();

    // ── Contenido ────────────────────────────────────────────────────────────

    /// <summary>Vuelve a escribir los textos y el estado del botón (al mostrar y al cambiar el código).</summary>
    public void Refrescar()
    {
        string codigo = CodigoParticipante.Actual;
        bool hayCodigo = codigo.Length > 0;

        if (textoCodigo != null) textoCodigo.text = hayCodigo ? codigo : placeholderCodigo;
        if (textoCuerpo != null) textoCuerpo.text = ArmarCuerpo();

        bool habilitado = hayCodigo;
        if (botonComenzar != null) botonComenzar.Interactuable = habilitado;
        // El BotonMenuFeedback maneja el color cuando el botón está activo; se apaga cuando está bloqueado (mismo patrón que PanelAnamnesis).
        if (_feedbackComenzar != null) _feedbackComenzar.enabled = habilitado;
        if (fondoBotonComenzar != null) fondoBotonComenzar.color = habilitado ? colorComenzarActivo : colorComenzarBloqueado;
        if (textoAyuda != null)
        {
            textoAyuda.text = _codigoRechazado ? ayudaCodigoInvalido : ayudaSinCodigo;
            textoAyuda.gameObject.SetActive(!habilitado || _codigoRechazado);
        }
    }

    private string ArmarCuerpo()
    {
        var sb = new System.Text.StringBuilder(480);
        sb.Append("QUÉ HARÁS\n");
        sb.Append("Atenderás un caso clínico con un paciente virtual: lo examinarás, le harás preguntas, pedirás exámenes y ")
          .Append("registrarás tu diagnóstico y tus indicaciones.");
        if (!forzarEvaluacion) sb.Append(" Antes elegirás el modo (Tutorial o Evaluación).");
        sb.Append("\n\nQUÉ SE GUARDA\n");
        sb.Append("Tus acciones y tus respuestas del caso y el resultado, junto con tu código anónimo. No se guarda tu nombre.");
        return sb.ToString();
    }

    // ── Código: teclado nativo (mismo patrón que InteraccionPaciente) ─────────────

    private void AbrirTeclado()
    {
        if (_teclado != null && _teclado.active) return; // ya hay uno abierto

        _teclado = TouchScreenKeyboard.Open(
            CodigoParticipante.Actual,           // muestra el código previo para editarlo
            TouchScreenKeyboardType.Default,
            autocorrection: false,
            multiline: false,
            secure: false,
            alert: false,
            textPlaceholder: "Tu código",
            characterLimit: CodigoParticipante.LARGO_MAX);
    }

    private void ProcesarTeclado()
    {
        switch (_teclado.status)
        {
            case TouchScreenKeyboard.Status.Visible:
                return; // el participante sigue escribiendo

            case TouchScreenKeyboard.Status.Done:
            {
                string texto = _teclado.text;
                _teclado = null;
                AplicarCodigoEscrito(texto);
                return;
            }

            default: // Canceled / LostFocus → no cambia nada
                _teclado = null;
                return;
        }
    }

    /// <summary>Procesa un código escrito (lo que confirma el teclado nativo): lo normaliza y lo guarda si es válido; si no,
    /// conserva el anterior y avisa. Público para poder probarlo sin teclado.</summary>
    public void AplicarCodigoEscrito(string texto)
    {
        _codigoRechazado = false;
        if (!string.IsNullOrWhiteSpace(texto) && !CodigoParticipante.Guardar(texto, out _)) _codigoRechazado = true;
        Refrescar();
    }

    // ── Comenzar ─────────────────────────────────────────────────────────────

    /// <summary>«Comenzar»: con forzarEvaluacion inicia un caso en Evaluación (como tocar la tarjeta); sin él, deja ver el Panel 2.
    /// Sin código válido no hace nada.</summary>
    public void Comenzar()
    {
        if (!CodigoParticipante.HayCodigo) { Refrescar(); return; }

        _yaComenzo = true;
        _pendiente = false;                 // antes de iniciar el caso, para que ningún cambio de estado vuelva a mostrar el panel
        AplicarVisibilidad();
        AlCambiarPendiente?.Invoke();

        if (forzarEvaluacion)
        {
            if (selectorCaso != null) selectorCaso.SeleccionarYComenzar(SubmodoCaso.Evaluacion);
            else Debug.LogError("[PanelIntroduccionDiagnostico] Falta SelectorCasoAleatorio.", this);
        }
    }
}
