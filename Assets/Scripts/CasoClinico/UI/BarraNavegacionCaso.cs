using UnityEngine;

/// <summary>
/// Barra de navegación compartida del caso: un panel head-locked pequeño (debajo
/// del panel principal) con "◀ Atrás" y "Salir del caso", disponible durante
/// todas las fases interactivas del caso — así cada panel no repite estos botones.
///
/// Visible en las fases de interacción con el paciente: ExamenFisico, Anamnesis,
/// ExamenesComplementarios, RegistroDiagnostico. Oculta en el resto (incluida
/// Introduccion — ahí el botón "Atrás" vive en el propio panel de introducción).
/// </summary>
public class BarraNavegacionCaso : MonoBehaviour
{
    [Header("Visual (con AnclajeCabeza)")]
    [SerializeField] private GameObject raiz;

    [Header("Botones")]
    [SerializeField] private SeleccionableToque botonAtras;
    [SerializeField] private SeleccionableToque botonSalir;

    [Header("Sistema")]
    [SerializeField] private GestorCasoClinico gestorCaso;

    void Awake()
    {
        if (raiz != null) raiz.SetActive(false);
    }

    void OnEnable()
    {
        if (botonAtras != null) botonAtras.OnSeleccionado += Atras;
        if (botonSalir != null) botonSalir.OnSeleccionado += Salir;
        if (gestorCaso != null) gestorCaso.OnCambioEstado += AlCambioEstado;
    }

    void OnDisable()
    {
        if (botonAtras != null) botonAtras.OnSeleccionado -= Atras;
        if (botonSalir != null) botonSalir.OnSeleccionado -= Salir;
        if (gestorCaso != null) gestorCaso.OnCambioEstado -= AlCambioEstado;
    }

    private void Atras() { if (gestorCaso != null) gestorCaso.RetrocederFase(); }
    private void Salir() { if (gestorCaso != null) gestorCaso.AbandonarCaso(); }

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        bool visible =
            estado == EstadoCasoClinico.ExamenFisico ||
            estado == EstadoCasoClinico.Anamnesis ||
            estado == EstadoCasoClinico.ExamenesComplementarios ||
            estado == EstadoCasoClinico.RegistroDiagnostico;

        if (raiz != null && raiz.activeSelf != visible) raiz.SetActive(visible);
    }
}
