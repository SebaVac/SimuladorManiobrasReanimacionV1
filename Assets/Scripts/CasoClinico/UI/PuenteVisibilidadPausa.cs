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
///     solo reenvía la orden. Los paneles se DESCUBREN solos (cualquier MonoBehaviour de la
///     escena que implemente la interfaz, activo o no); la lista serializada
///     <see cref="panelesOcultables"/> queda como respaldo — así reconstruir un panel con su
///     builder ya no puede dejar un panel sin ocultar por un slot nulo (2026-10-13).
/// </summary>
public class PuenteVisibilidadPausa : MonoBehaviour
{
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private GestorMenuPausa menuPausa;

    [Tooltip("RESPALDO. Los paneles que implementan IPanelOcultable se DESCUBREN solos en la escena " +
             "(incluidos los inactivos) cada vez que se abre la pausa; esta lista solo aporta los que " +
             "queden fuera de ese descubrimiento. Reconstruir un panel con su builder deja aquí un slot " +
             "nulo (ya pasó 3 veces): los nulos se ignoran, no hace falta repararlos a mano.")]
    [SerializeField] private List<MonoBehaviour> panelesOcultables = new List<MonoBehaviour>();

    // Conjunto efectivo = lista serializada (no nulos) ∪ descubiertos. Se reconstruye al
    // abrir la pausa (acción discreta, no por frame) y se reutiliza para restaurar.
    private readonly List<IPanelOcultable> _efectivos = new List<IPanelOcultable>();
    private readonly HashSet<MonoBehaviour> _vistos = new HashSet<MonoBehaviour>();

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
        ReconstruirEfectivos();
        for (int i = 0; i < _efectivos.Count; i++) _efectivos[i].OcultarTemporalmente();
    }

    private void RestaurarPaneles()
    {
        // Mismos paneles que se ocultaron (no se redescubre: un panel instanciado con el
        // menú abierto no tiene estado "oculto por pausa" que restaurar).
        for (int i = 0; i < _efectivos.Count; i++)
            if (_efectivos[i] is Object o && o != null) _efectivos[i].RestaurarVisibilidad();
    }

    private void ReconstruirEfectivos()
    {
        _efectivos.Clear();
        _vistos.Clear();

        // 1) Descubrimiento: todo MonoBehaviour de la escena que implemente IPanelOcultable,
        //    activo o no (los paneles ocultos por estado también deben recordar y restaurar).
        var todos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
            if (todos[i] is IPanelOcultable p && _vistos.Add(todos[i])) _efectivos.Add(p);

        // 2) Respaldo serializado: nulos (panel reconstruido) se ignoran; los demás se suman
        //    si el descubrimiento no los trajo.
        for (int i = 0; i < panelesOcultables.Count; i++)
            if (panelesOcultables[i] is IPanelOcultable p && _vistos.Add(panelesOcultables[i])) _efectivos.Add(p);
    }

    /// <summary>Cantidad de paneles que se ocultarían hoy (descubiertos + respaldo). Solo para
    /// verificación/depuración.</summary>
    public int ContarPanelesEfectivos()
    {
        ReconstruirEfectivos();
        return _efectivos.Count;
    }
}
