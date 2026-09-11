using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// (BotonMenuFeedback está en el namespace global del proyecto)

/// <summary>
/// Panel 5 — Anamnesis. Head-locked (AnclajeCabeza en <see cref="raiz"/>), mismo
/// patrón de activación que PanelIntroduccion / PanelSeleccionSubmodo / Panel 4 / 7:
/// el controlador queda siempre activo y alterna <see cref="raiz"/> según el estado.
///
/// Estética teal diegética, consistente con el resto del módulo.
///
/// Flujo: el jugador toca "Escribir una pregunta" → <see cref="InteraccionPaciente.AbrirTecladoAnamnesis"/>
/// (teclado nativo). Al confirmar, <see cref="GestorCasoClinico"/> resuelve el match
/// y dispara <see cref="GestorCasoClinico.OnIntercambioAnamnesis"/> con (pregunta,
/// respuesta). Este panel agrega esa dupla a un HISTORIAL ACUMULATIVO scrolleable,
/// sin borrar las anteriores. Repetir una pregunta agrega una entrada nueva.
///
/// Evaluación invisible: el historial NO marca si la pregunta fue reconocida o no
/// (se ve igual una respuesta específica que la genérica).
///
/// "Continuar a Exámenes Complementarios" arranca bloqueado
/// (<see cref="SeleccionableToque.Interactuable"/> = false) y se habilita cuando
/// <see cref="GestorCasoClinico.PreguntasReconocidasCount"/> ≥ 1 — es decir, tras
/// AL MENOS 1 pregunta DISTINTA que haya matcheado una PreguntaAnamnesis. Solo
/// respuestas genéricas no habilitan el botón.
///
/// Visible únicamente en <see cref="EstadoCasoClinico.Anamnesis"/>.
/// </summary>
public class PanelAnamnesis : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio (IPanelOcultable).
    private bool _visibleSegunEstado;

    [Header("Historial")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text textoHistorial;
    [Tooltip("Mensaje mientras el jugador aún no ha preguntado nada.")]
    [SerializeField] private string textoInicial = "Toca «Escribir una pregunta» y consultá al paciente.";
    [SerializeField] private SeleccionableToque botonScrollArriba;
    [SerializeField] private SeleccionableToque botonScrollAbajo;
    [Range(0.05f, 0.6f)]
    [SerializeField] private float pasoScroll = 0.25f;

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonPreguntar;
    [SerializeField] private SeleccionableToque botonContinuar;
    [Tooltip("Fondo del botón Continuar, para atenuarlo mientras está bloqueado.")]
    [SerializeField] private Image fondoBotonContinuar;
    [SerializeField] private Color colorContinuarActivo   = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    [SerializeField] private Color colorContinuarBloqueado = new Color(0.05f, 0.09f, 0.10f, 0.55f);

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;

    private readonly StringBuilder _historial = new StringBuilder(512);
    private int _entradas;

    // BotonMenuFeedback del botón Continuar: mientras el botón está bloqueado se
    // desactiva para que no pise el color atenuado con su propio lerp.
    private BotonMenuFeedback _feedbackContinuar;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        if (botonContinuar != null) _feedbackContinuar = botonContinuar.GetComponent<BotonMenuFeedback>();
        Reiniciar();
    }

    void OnEnable()
    {
        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado        += AlCambioEstado;
            gestorCaso.OnIntercambioAnamnesis += AlIntercambioAnamnesis;
        }
        if (botonPreguntar != null)      botonPreguntar.OnSeleccionado    += Preguntar;
        if (botonContinuar != null)      botonContinuar.OnSeleccionado    += Continuar;
        if (botonScrollArriba != null)   botonScrollArriba.OnSeleccionado += ScrollArriba;
        if (botonScrollAbajo != null)    botonScrollAbajo.OnSeleccionado  += ScrollAbajo;
    }

    void OnDisable()
    {
        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado        -= AlCambioEstado;
            gestorCaso.OnIntercambioAnamnesis -= AlIntercambioAnamnesis;
        }
        if (botonPreguntar != null)      botonPreguntar.OnSeleccionado    -= Preguntar;
        if (botonContinuar != null)      botonContinuar.OnSeleccionado    -= Continuar;
        if (botonScrollArriba != null)   botonScrollArriba.OnSeleccionado -= ScrollArriba;
        if (botonScrollAbajo != null)    botonScrollAbajo.OnSeleccionado  -= ScrollAbajo;
    }

    // ── Visibilidad ──────────────────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        // El historial se limpia al ARRANCAR un caso nuevo (Introduccion), no cada
        // vez que se entra a Anamnesis: así retroceder ExamenesComplementarios →
        // Anamnesis conserva lo preguntado, igual que MotorEvaluacion conserva sus
        // registros al retroceder de fase.
        if (estado == EstadoCasoClinico.Introduccion) Reiniciar();

        bool visible = estado == EstadoCasoClinico.Anamnesis;
        _visibleSegunEstado = visible;
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
        // Después de activar: así el estado atenuado gana sobre el BotonMenuFeedback.Awake
        // del botón Continuar (que fija su color al activarse el GameObject).
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
        _historial.Clear();
        _entradas = 0;
        if (textoHistorial != null) textoHistorial.text = textoInicial;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        ActualizarBotonContinuar();
    }

    // ── Preguntar / historial ────────────────────────────────────────────────

    private void Preguntar()
    {
        // Log de diagnóstico (BUG 1, 2026-09-11) — confirma en dispositivo que el
        // toque del botón SÍ llega hasta aquí. Se deja a propósito.
        Debug.Log("[PanelAnamnesis] Preguntar(): botón tocado, interaccionPaciente=" +
                  (interaccionPaciente != null ? "asignado" : "NULL"), this);
        if (interaccionPaciente != null) interaccionPaciente.AbrirTecladoAnamnesis();
    }

    private void AlIntercambioAnamnesis(string pregunta, string respuesta)
    {
        if (_entradas == 0) _historial.Clear(); // descarta el textoInicial
        else                _historial.Append("\n\n");

        _historial.Append("<b>Tú:</b> ").Append(pregunta)
                  .Append('\n')
                  .Append("<b>Paciente:</b> ").Append(respuesta);
        _entradas++;

        if (textoHistorial != null) textoHistorial.text = _historial.ToString();

        ActualizarBotonContinuar();
        ScrollAlFinal();
    }

    private void ScrollAlFinal()
    {
        if (scroll == null) return;
        Canvas.ForceUpdateCanvases();            // recalcula el layout antes de scrollear
        scroll.verticalNormalizedPosition = 0f;  // 0 = abajo (entrada más reciente)
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
        bool habilitado = gestorCaso != null && gestorCaso.PreguntasReconocidasCount >= 1;
        if (botonContinuar != null) botonContinuar.Interactuable = habilitado;

        // El BotonMenuFeedback maneja el color cuando el botón está activo; se
        // apaga cuando está bloqueado para que el color atenuado no sea pisado.
        if (_feedbackContinuar != null) _feedbackContinuar.enabled = habilitado;
        if (fondoBotonContinuar != null)
            fondoBotonContinuar.color = habilitado ? colorContinuarActivo : colorContinuarBloqueado;
    }

    private void Continuar()
    {
        // El botón ya está bloqueado sin ≥1 pregunta reconocida; doble guard por las dudas.
        if (gestorCaso != null && gestorCaso.PreguntasReconocidasCount >= 1)
            gestorCaso.AvanzarFase(); // Anamnesis → ExamenesComplementarios
    }
}
