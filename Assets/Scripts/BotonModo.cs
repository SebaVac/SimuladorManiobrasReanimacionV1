using UnityEngine;

public class BotonModo : MonoBehaviour
{
    public CalibradorPosicion scriptCalibrador; // Arrastra aquí el GestorRCP
    public Renderer miRenderer;

    // Colores para saber en qué estado está
    public Color colorModoCuerpo = Color.blue;
    public Color colorModoSensor = Color.green;

    private void Start()
    {
        ActualizarColor(false); // Empezamos en modo cuerpo (False)
    }

    // Unity detecta automáticamente cuando otro objeto (tu mano) entra en este cubo
    private void OnTriggerEnter(Collider other)
    {
        // Verificamos si lo que tocó el botón es una mano o parte del jugador
        // (Aceptamos cualquier cosa que tenga un Collider por simplicidad)
        if (scriptCalibrador != null)
        {
            scriptCalibrador.AlternarModo(); // Cambiamos el modo

            // Efecto visual de "Click"
            bool nuevoEstado = scriptCalibrador.ObtenerEstadoModo(); // Necesitamos añadir esto al otro script
            ActualizarColor(nuevoEstado);

            // Opcional: Sonido o vibración aquí
            Debug.Log("?? ¡Botón presionado!");
        }
    }

    void ActualizarColor(bool esModoSensor)
    {
        if (miRenderer != null)
        {
            miRenderer.material.color = esModoSensor ? colorModoSensor : colorModoCuerpo;
        }
    }
}