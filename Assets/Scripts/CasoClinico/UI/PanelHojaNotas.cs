using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hoja de notas del jugador — espacio personal, sin evaluación ni conexión a
/// MotorEvaluacion. Head-locked (AnclajeCabeza en <see cref="raiz"/>), mismo
/// patrón de activación que los demás paneles del módulo.
///
/// Teclado nativo AUTOCONTENIDO en este mismo componente (no en
/// InteraccionPaciente): las notas no son parte de la interacción con el
/// paciente, y acoplarlas ahí obligaría a InteraccionPaciente a conocer un
/// concepto ajeno a su responsabilidad. El polling replica el mismo patrón ya
/// probado (<see cref="InteraccionPaciente.AbrirTeclado"/>), sin el logging de
/// diagnóstico que ahí es específico del Bug 1 del teclado.
///
/// Contenido: nota de texto libre ACUMULATIVA (como el historial de Anamnesis,
/// sin estructura pregunta/respuesta) — reabrir el teclado agrega texto, nunca
/// reemplaza. Crece dentro de un ScrollRect (mismo patrón de Panel 5/6); esto
/// evita el bug de overflow del Panel 7 por diseño, no por autoSizing: el
/// contenido no tiene techo de altura fijo, así que no hay "desborde" posible
/// — se scrollea en vez de shrinkear la fuente indefinidamente.
///
/// Visible en ExamenFisico, Anamnesis, ExamenesComplementarios y
/// RegistroDiagnostico. Oculta en Introduccion/Inactivo/Evaluando/Debriefing.
/// Se limpia al iniciar un caso nuevo (mismo criterio que el resto del estado
/// del caso: OnCambioEstado(Introduccion) → Reiniciar()).
/// </summary>
public class PanelHojaNotas : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio (IPanelOcultable).
    private bool _visibleSegunEstado;

    [Header("Contenido acumulativo")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private TMP_Text textoNotas;
    [Tooltip("Mensaje mientras el jugador aún no escribió ninguna nota.")]
    [SerializeField] private string textoInicial = "Toca «Escribir nota» para anotar algo.";
    [SerializeField] private SeleccionableToque botonScrollArriba;
    [SerializeField] private SeleccionableToque botonScrollAbajo;
    [Range(0.05f, 0.6f)]
    [SerializeField] private float pasoScroll = 0.25f;

    [Header("Interacción")]
    [SerializeField] private SeleccionableToque botonEscribir;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;

    private readonly StringBuilder _notas = new StringBuilder(512);
    private int _entradas;

    // ── Teclado nativo autocontenido (mismo patrón que InteraccionPaciente.AbrirTeclado) ──
    private TouchScreenKeyboard _teclado;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
        Reiniciar();
    }

    void OnEnable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
        if (botonEscribir != null) botonEscribir.OnSeleccionado += AbrirTeclado;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado += ScrollArriba;
        if (botonScrollAbajo != null) botonScrollAbajo.OnSeleccionado += ScrollAbajo;
    }

    void OnDisable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
        if (botonEscribir != null) botonEscribir.OnSeleccionado -= AbrirTeclado;
        if (botonScrollArriba != null) botonScrollArriba.OnSeleccionado -= ScrollArriba;
        if (botonScrollAbajo != null) botonScrollAbajo.OnSeleccionado -= ScrollAbajo;
    }

    void Update()
    {
        ProcesarTeclado(); // acción discreta — se atiende aunque el panel esté oculto un frame
    }

    // ── Visibilidad ──────────────────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        // Mismo criterio que el resto del estado del caso: se limpia solo al
        // ARRANCAR un caso nuevo (Introduccion), no cada vez que se re-entra a
        // una fase interactiva.
        if (estado == EstadoCasoClinico.Introduccion) Reiniciar();

        bool visible =
            estado == EstadoCasoClinico.ExamenFisico ||
            estado == EstadoCasoClinico.Anamnesis ||
            estado == EstadoCasoClinico.ExamenesComplementarios ||
            estado == EstadoCasoClinico.RegistroDiagnostico;

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

    private void Reiniciar()
    {
        _notas.Clear();
        _entradas = 0;
        if (textoNotas != null) textoNotas.text = textoInicial;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    // ── Notas ────────────────────────────────────────────────────────────────

    private void AbrirTeclado()
    {
        if (_teclado != null && _teclado.active) return; // ya hay uno abierto

        _teclado = TouchScreenKeyboard.Open(
            "",
            TouchScreenKeyboardType.Default,
            autocorrection: false,
            multiline: true,
            secure: false,
            alert: false,
            textPlaceholder: "Escribí tu nota…");
    }

    private void ProcesarTeclado()
    {
        if (_teclado == null) return;

        switch (_teclado.status)
        {
            case TouchScreenKeyboard.Status.Visible:
                return; // el jugador sigue escribiendo

            case TouchScreenKeyboard.Status.Done:
            {
                string texto = _teclado.text;
                _teclado = null;
                if (!string.IsNullOrWhiteSpace(texto)) AgregarNota(texto);
                return;
            }

            default: // Canceled / LostFocus → no agrega nada
                _teclado = null;
                return;
        }
    }

    private void AgregarNota(string texto)
    {
        if (_entradas == 0) _notas.Clear(); // descarta el textoInicial
        else                _notas.Append("\n\n");

        _notas.Append(texto);
        _entradas++;

        if (textoNotas != null) textoNotas.text = _notas.ToString();
        ScrollAlFinal();
    }

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
}
