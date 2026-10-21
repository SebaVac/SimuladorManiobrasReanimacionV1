using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel 7 — Registro de Diagnóstico. Anclado a un slot fijo de la sala (SlotPanel; <see cref="raiz"/> es hija del slot),
/// mismo patrón de activación que PanelIntroduccion / PanelSeleccionSubmodo: el
/// controlador queda siempre activo y alterna <see cref="raiz"/> según el estado.
///
/// Estética DELIBERADAMENTE distinta al resto del módulo: ficha clínica de papel
/// (crema/beige, tinta oscura, bordes finos), no el teal diegético. Es intencional
/// — no unificar con los demás paneles.
///
/// Dos campos de texto libre interactivos, mismo comportamiento:
///   - "Diagnóstico": tocarlo abre el teclado nativo
///     (<see cref="InteraccionPaciente.AbrirTecladoDiagnostico"/>).
///   - "Indicaciones / manejo": ídem con <see cref="InteraccionPaciente.AbrirTecladoManejo"/>.
///   Ambos se pueden reescribir sin límite antes de registrar.
///   - "Registrar diagnóstico": si hay diagnóstico, cierra el caso
///     (<see cref="GestorCasoClinico.OnJugadorRegistroDiagnostico"/> con el diagnóstico y el
///     manejo escritos). El manejo puede quedar vacío (suma 0); el diagnóstico NO: si está
///     vacío/null, resalta su campo y no hace nada.
///
/// Visible únicamente en <see cref="EstadoCasoClinico.RegistroDiagnostico"/>.
/// Evaluación invisible: nunca muestra correcto/incorrecto.
/// </summary>
public class PanelRegistroDiagnostico : MonoBehaviour, IPanelOcultable
{
    [Header("Visual (raíz hija de un SlotPanel)")]
    [SerializeField] private GameObject raiz;

    // Ver PanelIntroduccion._visibleSegunEstado — mismo criterio (IPanelOcultable).
    private bool _visibleSegunEstado;

    [Header("Campos de solo lectura")]
    [SerializeField] private TMP_Text textoPaciente;
    [SerializeField] private TMP_Text textoSexo;
    [SerializeField] private TMP_Text textoEdad;
    [SerializeField] private TMP_Text textoEstablecimiento;
    [SerializeField] private TMP_Text textoFechaHora;

    [Header("Campo de diagnóstico (interactivo)")]
    [SerializeField] private SeleccionableToque campoDiagnostico;
    [SerializeField] private TMP_Text textoDiagnostico;
    [SerializeField] private Image fondoCampoDiagnostico;
    [SerializeField] private Image bordeCampoDiagnostico;

    [Header("Campo de indicaciones / manejo (interactivo)")]
    [SerializeField] private SeleccionableToque campoManejo;
    [SerializeField] private TMP_Text textoManejo;
    [SerializeField] private Image bordeCampoManejo;

    [Header("Registrar")]
    [SerializeField] private SeleccionableToque botonRegistrar;
    [SerializeField] private Image bordeBotonRegistrar;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;

    [Header("Textos / formato")]
    [SerializeField] private string placeholderDiagnostico = "Toca para escribir el diagnóstico";
    [SerializeField] private string placeholderManejo = "Toca para escribir las indicaciones";
    [SerializeField] private string formatoFechaHora = "dd/MM/yyyy · HH:mm";

    // ── Paleta ficha de papel ────────────────────────────────────────────────
    private static readonly Color TINTA        = new Color(0.16f, 0.13f, 0.10f, 1f);
    private static readonly Color TINTA_TENUE  = new Color(0.42f, 0.36f, 0.30f, 1f);
    private static readonly Color CAMPO_NORMAL = new Color(0.99f, 0.97f, 0.90f, 1f);
    private static readonly Color CAMPO_ALERTA = new Color(0.96f, 0.85f, 0.70f, 1f);
    private static readonly Color BORDE_NORMAL = new Color(0.30f, 0.26f, 0.20f, 1f);
    private static readonly Color BORDE_ACTIVO = new Color(0.55f, 0.42f, 0.20f, 1f);

    private Coroutine _alertaActiva;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    void OnEnable()
    {
        if (campoDiagnostico != null) campoDiagnostico.OnSeleccionado += AbrirTeclado;
        if (campoManejo != null)      campoManejo.OnSeleccionado      += AbrirTecladoManejo;
        if (botonRegistrar != null)   botonRegistrar.OnSeleccionado   += IntentarRegistrar;
        if (gestorCaso != null)       gestorCaso.OnCambioEstado        += AlCambioEstado;
        if (interaccionPaciente != null)
        {
            interaccionPaciente.OnDiagnosticoTextoEnviado += AlEscribirDiagnostico;
            interaccionPaciente.OnManejoTextoEnviado      += AlEscribirManejo;
        }
    }

    void OnDisable()
    {
        if (campoDiagnostico != null) campoDiagnostico.OnSeleccionado -= AbrirTeclado;
        if (campoManejo != null)      campoManejo.OnSeleccionado      -= AbrirTecladoManejo;
        if (botonRegistrar != null)   botonRegistrar.OnSeleccionado   -= IntentarRegistrar;
        if (gestorCaso != null)       gestorCaso.OnCambioEstado        -= AlCambioEstado;
        if (interaccionPaciente != null)
        {
            interaccionPaciente.OnDiagnosticoTextoEnviado -= AlEscribirDiagnostico;
            interaccionPaciente.OnManejoTextoEnviado      -= AlEscribirManejo;
        }
    }

    void Update()
    {
        // Realce sepia mínimo del borde al tocar (sin BotonMenuFeedback, que impone
        // el teal del resto de la UI). Cero-alloc.
        if (raiz == null || !raiz.activeSelf) return;

        if (bordeCampoDiagnostico != null && campoDiagnostico != null)
            bordeCampoDiagnostico.color = campoDiagnostico.EnContacto ? BORDE_ACTIVO : BORDE_NORMAL;
        if (bordeCampoManejo != null && campoManejo != null)
            bordeCampoManejo.color = campoManejo.EnContacto ? BORDE_ACTIVO : BORDE_NORMAL;
        if (bordeBotonRegistrar != null && botonRegistrar != null)
            bordeBotonRegistrar.color = botonRegistrar.EnContacto ? BORDE_ACTIVO : BORDE_NORMAL;
    }

    // ── Visibilidad + contenido ──────────────────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        bool visible = estado == EstadoCasoClinico.RegistroDiagnostico;
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
        var datos = caso != null ? caso.datosAdministrativos : null;

        Asignar(textoPaciente,        datos != null ? datos.nombrePacienteFicticio : null);
        Asignar(textoSexo,            datos != null ? datos.sexoPaciente            : null);
        Asignar(textoEdad,            datos != null ? datos.edadPaciente            : null);
        Asignar(textoEstablecimiento, datos != null ? datos.nombreEstablecimiento  : null);

        if (textoFechaHora != null && gestorCaso != null)
            textoFechaHora.text = gestorCaso.FechaHoraInicio.ToString(
                formatoFechaHora, CultureInfo.InvariantCulture);

        RefrescarCampoDiagnostico(gestorCaso != null ? gestorCaso.DiagnosticoRegistrado : null);
        RefrescarCampoManejo(gestorCaso != null ? gestorCaso.ManejoRegistrado : null);

        if (fondoCampoDiagnostico != null) fondoCampoDiagnostico.color = CAMPO_NORMAL;
    }

    private static void Asignar(TMP_Text campo, string valor)
    {
        if (campo != null) campo.text = string.IsNullOrEmpty(valor) ? "—" : valor;
    }

    // ── Campo de diagnóstico ─────────────────────────────────────────────────

    private void AbrirTeclado()
    {
        if (interaccionPaciente != null) interaccionPaciente.AbrirTecladoDiagnostico();
    }

    // Se dispara cada vez que el jugador confirma texto en el teclado — sin límite.
    private void AlEscribirDiagnostico(string texto) => RefrescarCampoDiagnostico(texto);

    private void RefrescarCampoDiagnostico(string dx)
    {
        if (textoDiagnostico == null) return;

        bool vacio = string.IsNullOrWhiteSpace(dx);
        textoDiagnostico.text      = vacio ? placeholderDiagnostico : dx;
        textoDiagnostico.color     = vacio ? TINTA_TENUE : TINTA;
        textoDiagnostico.fontStyle = vacio ? FontStyles.Italic : FontStyles.Normal;
    }

    // ── Campo de indicaciones / manejo (mismo patrón que el de diagnóstico) ───────

    private void AbrirTecladoManejo()
    {
        if (interaccionPaciente != null) interaccionPaciente.AbrirTecladoManejo();
    }

    private void AlEscribirManejo(string texto) => RefrescarCampoManejo(texto);

    private void RefrescarCampoManejo(string manejo)
    {
        if (textoManejo == null) return;

        bool vacio = string.IsNullOrWhiteSpace(manejo);
        textoManejo.text      = vacio ? placeholderManejo : manejo;
        textoManejo.color     = vacio ? TINTA_TENUE : TINTA;
        textoManejo.fontStyle = vacio ? FontStyles.Italic : FontStyles.Normal;
    }

    // ── Registrar ────────────────────────────────────────────────────────────

    private void IntentarRegistrar()
    {
        if (gestorCaso == null) return;

        if (string.IsNullOrWhiteSpace(gestorCaso.DiagnosticoRegistrado))
        {
            if (isActiveAndEnabled)
            {
                if (_alertaActiva != null) StopCoroutine(_alertaActiva);
                _alertaActiva = StartCoroutine(DestelloAlerta());
            }
            return; // no se puede cerrar el caso sin diagnóstico
        }

        // El manejo puede quedar vacío (suma 0): solo el diagnóstico es obligatorio.
        gestorCaso.OnJugadorRegistroDiagnostico(gestorCaso.DiagnosticoRegistrado, gestorCaso.ManejoRegistrado);
    }

    private IEnumerator DestelloAlerta()
    {
        if (fondoCampoDiagnostico != null) fondoCampoDiagnostico.color = CAMPO_ALERTA;
        yield return new WaitForSeconds(0.6f);
        if (fondoCampoDiagnostico != null) fondoCampoDiagnostico.color = CAMPO_NORMAL;
        _alertaActiva = null;
    }
}
