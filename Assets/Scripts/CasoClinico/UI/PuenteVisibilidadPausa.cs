using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puente escena-específico (SceneDiagnostico) entre el módulo de razonamiento
/// clínico y <see cref="GestorMenuPausa"/> (compartido con SceneSimulador, que no
/// conoce nada de este módulo). Dos responsabilidades:
///
///  1. Habilita el ícono/menú de pausa solo durante las fases de interacción con
///     el paciente — NO en la selección de submodo (Inactivo) ni en el Panel de
///     Introducción (ahí choca con el panel y la navegación "atrás" ya vive en el
///     propio panel). Coincide con la visibilidad de <see cref="BarraNavegacionCaso"/>.
///
///  2. BUG 2 (2026-09-11): al abrir/cerrar el menú de pausa, oculta/restaura el
///     panel del módulo que esté activo en ese momento — sin que ese panel (ni
///     GestorMenuPausa) necesite conocerse entre sí. Cada panel implementa
///     <see cref="IPanelOcultable"/> y recuerda su propio estado real; este puente
///     solo reenvía la orden a todos los registrados en <see cref="panelesOcultables"/>.
/// </summary>
public class PuenteVisibilidadPausa : MonoBehaviour
{
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private GestorMenuPausa menuPausa;

    [Tooltip("Los 6 controladores de panel del módulo (PanelIntroduccion, PanelSeleccionSubmodo, " +
             "PanelHallazgos, PanelAnamnesis, PanelExamenesComplementarios, PanelRegistroDiagnostico). " +
             "Cada uno debe implementar IPanelOcultable; los que no lo implementen se ignoran.")]
    [SerializeField] private List<MonoBehaviour> panelesOcultables = new List<MonoBehaviour>();

    void OnEnable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += OcultarPaneles;
            menuPausa.AlCerrarMenu += RestaurarPaneles;
        }
        Aplicar(gestorCaso != null ? gestorCaso.EstadoActual : EstadoCasoClinico.Inactivo);
    }

    void OnDisable()
    {
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= OcultarPaneles;
            menuPausa.AlCerrarMenu -= RestaurarPaneles;
        }
    }

    private void AlCambioEstado(EstadoCasoClinico estado) => Aplicar(estado);

    private void Aplicar(EstadoCasoClinico estado)
    {
        bool enFaseInteractiva =
            estado == EstadoCasoClinico.ExamenFisico ||
            estado == EstadoCasoClinico.Anamnesis ||
            estado == EstadoCasoClinico.ExamenesComplementarios ||
            estado == EstadoCasoClinico.RegistroDiagnostico;

        if (menuPausa != null)
            menuPausa.HabilitarPausa(enFaseInteractiva);
    }

    // ── BUG 2: ocultar/restaurar el panel activo mientras el menú está abierto ──

    private void OcultarPaneles()
    {
        for (int i = 0; i < panelesOcultables.Count; i++)
            if (panelesOcultables[i] is IPanelOcultable p) p.OcultarTemporalmente();
    }

    private void RestaurarPaneles()
    {
        for (int i = 0; i < panelesOcultables.Count; i++)
            if (panelesOcultables[i] is IPanelOcultable p) p.RestaurarVisibilidad();
    }
}
