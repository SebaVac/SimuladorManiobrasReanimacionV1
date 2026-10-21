using UnityEngine;

/// <summary>
/// Datos que guarda ConstruirAmbientacionUrgencias en su root "AmbientacionUrgencias" para poder restaurar la escena
/// tal como estaba antes de la ambientación (materiales originales de las piezas del pack y valores de
/// LucesBoxConsulta). Solo datos serializados: no tiene lógica ni Update.
/// </summary>
public class RegistroAmbientacion : MonoBehaviour
{
    public Renderer[] renderers;
    public Material[] materialesOriginales;

    public Color colorLuz;
    public float intensidadPrincipal;
    public float intensidadPuntual;
    public Color colorAmbiente;
    public Component luces;      // el LucesBoxConsulta al que pertenecen los valores
    public Transform luzPrincipal;
    public Quaternion rotacionLuzPrincipal;
}
