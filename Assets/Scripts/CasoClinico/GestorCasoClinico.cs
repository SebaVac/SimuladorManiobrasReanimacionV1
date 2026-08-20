using UnityEngine;

/// <summary>
/// Mantiene la referencia al caso clínico activo y la expone a otros sistemas.
/// Punto de acceso único para MotorEvolucionPaciente — desacoplado de lógica de evaluación.
/// Asignar el FichaCaso en el Inspector o llamar a CargarCaso() en tiempo de ejecución.
/// </summary>
public class GestorCasoClinico : MonoBehaviour
{
    [Header("Caso activo (asignar en Inspector)")]
    public FichaCaso casoActivo;

    public FichaCaso ObtenerCasoActivo() => casoActivo;

    // Permite cambiar el caso en tiempo de ejecución (ej: selección de caso desde menú)
    public void CargarCaso(FichaCaso nuevoCaso) => casoActivo = nuevoCaso;
}
