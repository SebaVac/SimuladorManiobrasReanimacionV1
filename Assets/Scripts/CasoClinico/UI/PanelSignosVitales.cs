using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel 3 — Monitor de signos vitales. Panel persistente y de solo lectura:
/// visible en ExamenFisico, Anamnesis, ExamenesComplementarios y
/// RegistroDiagnostico; oculto en Inactivo, Introduccion, Evaluando y Debriefing. NO agrega
/// estados a <see cref="GestorCasoClinico"/>, no usa SeleccionableToque, no habla con
/// MotorEvaluacion y no suma puntaje.
///
/// Muestra FC, FR, PA, SatO2 y temperatura de <c>CasoActual.signosVitales</c>, todas con el
/// mismo color (no marca los anormales). No muestra nombre del caso ni diagnóstico.
///
/// Los valores se refrescan al iniciar un caso (transición a Introduccion, ya con el caso
/// cargado) y se conservan al retroceder de fase. Implementa <see cref="IPanelOcultable"/>:
/// la pausa lo oculta y lo restaura al estado exacto calculado por la máquina de estados.
/// </summary>
public class PanelSignosVitales : MonoBehaviour, IPanelOcultable
{
    public const int FILAS = 5; // FC, FR, PA, SatO2, Temperatura

    [Header("Visual (raíz hija de un SlotPanel)")]
    [SerializeField] private GameObject raiz;

    [Header("Valores (FC, FR, PA, SatO2, Temperatura)")]
    [SerializeField] private TMP_Text[] valores = new TMP_Text[FILAS];

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;

    private bool _visibleSegunEstado;
    private FichaCaso _casoMostrado;

    /// <summary>Solo lectura/verificación: ¿debería verse según la máquina de estados?</summary>
    public bool VisibleSegunEstado => _visibleSegunEstado;

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    void OnEnable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
    }

    void OnDisable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
    }

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        // Refresco al iniciar un caso: IniciarCaso fija CasoActual y transiciona a Introduccion.
        // También si, por cualquier vía, se llegó a una fase visible con otro caso.
        var caso = gestorCaso != null ? gestorCaso.CasoActual : null;
        if (estado == EstadoCasoClinico.Introduccion || caso != _casoMostrado) Refrescar(caso);

        bool visible =
            estado == EstadoCasoClinico.ExamenFisico ||
            estado == EstadoCasoClinico.Anamnesis ||
            estado == EstadoCasoClinico.ExamenesComplementarios ||
            estado == EstadoCasoClinico.RegistroDiagnostico;

        _visibleSegunEstado = visible;
        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
    }

    private void Refrescar(FichaCaso caso)
    {
        _casoMostrado = caso;
        var sv = caso != null ? caso.signosVitales : null;
        Escribir(0, sv != null ? sv.frecuenciaCardiaca.ToString(CultureInfo.InvariantCulture) : "--");
        Escribir(1, sv != null ? sv.frecuenciaRespiratoria.ToString(CultureInfo.InvariantCulture) : "--");
        Escribir(2, sv != null && !string.IsNullOrWhiteSpace(sv.presionArterial) ? sv.presionArterial.Trim() : "--");
        Escribir(3, sv != null ? Decimal(sv.saturacionO2, "0.#") : "--");
        Escribir(4, sv != null ? Decimal(sv.temperatura, "0.0") : "--");
    }

    private void Escribir(int i, string texto)
    {
        if (valores != null && i < valores.Length && valores[i] != null) valores[i].text = texto;
    }

    // Decimal con coma (español), independiente de la cultura del dispositivo: 36,7
    private static string Decimal(float v, string formato) =>
        v.ToString(formato, CultureInfo.InvariantCulture).Replace('.', ',');

    // ── IPanelOcultable ──────────────────────────────────────────────────────

    public void OcultarTemporalmente()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    public void RestaurarVisibilidad()
    {
        if (raiz != null) raiz.SetActive(_visibleSegunEstado);
    }
}
