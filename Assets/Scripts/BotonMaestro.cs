using UnityEngine;
using TMPro; // Para cambiar texto del botón si quieres

public class BotonMaestro : MonoBehaviour
{
    [Header("Scripts a Controlar")]
    public CalibradorPosicion scriptCalibrador; // El que mueve el cuerpo
    public LogicaRCP scriptJuego;               // El que hace la simulación

    [Header("Visuales del Botón")]
    public Renderer miRenderer;
    public Color colorModoConfig = Color.blue; // Azul = Configurando
    public Color colorModoJuego = Color.red;   // Rojo = Jugando (No tocar configuración)

    [Header("Texto Informativo (Opcional)")]
    public TextMeshPro textoEstado;

    // ESTADO GLOBAL
    private bool enModoConfiguracion = true; // Empezamos configurando

    void Start()
    {
        AplicarEstado();
    }

    // Se activa al tocar el cubo con la mano (dedo índice con física)
    private void OnTriggerEnter(Collider other)
    {
        // Cambiamos el estado (Toggle)
        enModoConfiguracion = !enModoConfiguracion;
        AplicarEstado();

        // Debug
        Debug.Log("Cambio de Modo Global: " + (enModoConfiguracion ? "CONFIGURACIÓN" : "RCP"));
    }

    void AplicarEstado()
    {
        // 1. CONFIGURAMOS EL CALIBRADOR
        // Si estamos en config, el calibrador FUNCIONA (true). Si no, se apaga.
        if (scriptCalibrador != null)
        {
            scriptCalibrador.SetSistemaActivo(enModoConfiguracion);
        }

        // 2. CONFIGURAMOS LA LÓGICA RCP
        // Si estamos en config, la lógica está en MODO CALIBRACIÓN (Pausa).
        // Si no, la lógica empieza a jugar (modoCalibracion = false).
        if (scriptJuego != null)
        {
            scriptJuego.modoCalibracion = enModoConfiguracion;
        }

        // 3. CAMBIAMOS EL COLOR DEL BOTÓN
        if (miRenderer != null)
        {
            miRenderer.material.color = enModoConfiguracion ? colorModoConfig : colorModoJuego;
        }

        // 4. TEXTO OPCIONAL
        if (textoEstado != null)
        {
            textoEstado.text = enModoConfiguracion ? "Estado: CONFIGURACIÓN" : "Estado: RCP ACTIVO";
            textoEstado.color = enModoConfiguracion ? colorModoConfig : colorModoJuego;
        }
    }
}