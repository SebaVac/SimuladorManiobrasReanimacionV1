using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de introducción de SceneSimulador (protocolo de estudio). Aparece al cargar la escena, antes de calibrar:
/// explica qué se hará y qué se guarda, pide el código anónimo del participante (campo que abre el teclado nativo del
/// visor, con el mismo patrón de TouchScreenKeyboard de InteraccionPaciente) y tiene un botón
/// «Comenzar» que queda gris y deshabilitado, con un texto de ayuda, mientras no haya un código válido.
///
/// Mientras esté visible NO se cuentan compresiones: se mantiene LogicaRCP en pausa (SetPausado) — también al volver de
/// abrir/cerrar el menú de pausa, que reanuda la lógica por su cuenta — y se libera al pulsar «Comenzar». Al
/// comenzar el panel se oculta y la escena queda como estaba (Calibración al cargar).
///
/// Se oculta con el menú de pausa y se restaura igual (<see cref="IPanelOcultable"/>, suscripción directa a
/// <see cref="GestorMenuPausa"/>: en SceneSimulador no existe PuenteVisibilidadPausa). Sin trabajo por fotograma salvo
/// atender el teclado nativo cuando está abierto. Los textos se arman solo al mostrar o al cambiar el código.
/// </summary>
public class PanelIntroduccionRCP : MonoBehaviour, IPanelOcultable
{
    [Header("Protocolo")]
    [Tooltip("Muestra el panel de introducción al cargar la escena. En false la escena arranca como antes (sin panel y sin pedir código).")]
    [SerializeField] private bool mostrarIntroduccion = true;

    [Header("Referencias")]
    [SerializeField] private LogicaRCP logica;
    [SerializeField] private SesionRCP sesion;
    [SerializeField] private PanelResultadosRCP resultados;
    [SerializeField] private GestorMenuPausa menuPausa;
    [Tooltip("Hijo que se activa/desactiva (el controlador queda siempre activo).")]
    [SerializeField] private GameObject raiz;

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

    private bool _visibleSegunEstado;
    private bool _pausoLogica;
    private bool _codigoRechazado;
    private TouchScreenKeyboard _teclado;
    private BotonMenuFeedback _feedbackComenzar;

    /// <summary>El panel debe verse (independiente de que el menú de pausa lo oculte un momento).</summary>
    public bool Pendiente => _visibleSegunEstado;
    public bool MostrarIntroduccion => mostrarIntroduccion;
    public bool ComenzarHabilitado => botonComenzar != null && botonComenzar.Interactuable;

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
        if (botonCodigo != null) botonCodigo.OnSeleccionado -= AbrirTeclado;
        if (botonComenzar != null) botonComenzar.OnSeleccionado -= Comenzar;
    }

    void Start()
    {
        _visibleSegunEstado = mostrarIntroduccion;
        if (_visibleSegunEstado)
        {
            Refrescar();
            AplicarVisibilidad(true);
        }
    }

    void Update()
    {
        if (_teclado != null) ProcesarTeclado(); // solo mientras el teclado nativo está abierto
    }

    // ── IPanelOcultable (menú de pausa) ──────────────────────────────────────

    public void OcultarTemporalmente()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    public void RestaurarVisibilidad()
    {
        // El menú ya reanudó LogicaRCP (onCerrarMenu se dispara antes que AlCerrarMenu): si el panel sigue pendiente se
        // vuelve a pausar, para que no se cuenten compresiones hasta que el participante pulse «Comenzar».
        AplicarVisibilidad(_visibleSegunEstado);
    }

    private void AplicarVisibilidad(bool visible)
    {
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
        if (logica == null) return;
        if (_visibleSegunEstado) { logica.SetPausado(true); _pausoLogica = true; }   // pendiente: lógica congelada
        else if (_pausoLogica) { logica.SetPausado(false); _pausoLogica = false; }   // «Comenzar»: se libera
    }

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
        // El BotonMenuFeedback maneja el color cuando el botón está activo; se apaga cuando está bloqueado para que el
        // color atenuado no sea pisado (mismo patrón que PanelAnamnesis).
        if (_feedbackComenzar != null) _feedbackComenzar.enabled = habilitado;
        if (fondoBotonComenzar != null) fondoBotonComenzar.color = habilitado ? colorComenzarActivo : colorComenzarBloqueado;
        if (textoAyuda != null)
        {
            // El aviso de código inválido se ve aunque ya haya otro guardado (se conserva el anterior).
            textoAyuda.text = _codigoRechazado ? ayudaCodigoInvalido : ayudaSinCodigo;
            textoAyuda.gameObject.SetActive(!habilitado || _codigoRechazado);
        }
    }

    private string ArmarCuerpo()
    {
        bool protocolo = resultados != null && resultados.ModoProtocolo;
        int n = resultados != null ? resultados.SesionesPorParticipante : 1;
        float dur = sesion != null ? sesion.DuracionSesionSeg : 120f;

        var sb = new System.Text.StringBuilder(320);
        sb.Append("QUÉ HARÁS\n");
        sb.Append("1. Calibrarás la posición del paciente.\n");
        if (protocolo)
            sb.Append("2. Harás ").Append(n).Append(n == 1 ? " sesión" : " sesiones").Append(" de compresiones de ")
              .Append(FormatoDuracion(dur)).Append(n == 1 ? "." : " cada una.");
        else
            sb.Append("2. Practicarás compresiones en sesiones de ").Append(FormatoDuracion(dur)).Append('.');
        sb.Append("\n\nQUÉ SE GUARDA\n");
        if (protocolo)
            sb.Append("Tus resultados de cada sesión, junto con tu código anónimo. No se guarda tu nombre.");
        else
            sb.Append("En esta configuración no se guardan resultados.");
        return sb.ToString();
    }

    private static string FormatoDuracion(float seg)
    {
        int s = Mathf.RoundToInt(seg);
        if (s % 60 == 0) { int m = s / 60; return m + (m == 1 ? " minuto" : " minutos"); }
        return s + " segundos";
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

    /// <summary>Procesa un código escrito (lo que confirma el teclado nativo): lo normaliza y lo guarda si es válido; si
    /// no, conserva el anterior y avisa. Público para poder probarlo sin teclado.</summary>
    public void AplicarCodigoEscrito(string texto)
    {
        _codigoRechazado = false;
        if (!string.IsNullOrWhiteSpace(texto) && !CodigoParticipante.Guardar(texto, out _)) _codigoRechazado = true;
        Refrescar();
    }

    // ── Comenzar ─────────────────────────────────────────────────────────────

    /// <summary>«Comenzar»: oculta el panel y libera LogicaRCP. Sin código válido no hace nada.</summary>
    public void Comenzar()
    {
        if (!CodigoParticipante.HayCodigo) { Refrescar(); return; }
        _visibleSegunEstado = false;
        AplicarVisibilidad(false);
    }
}
