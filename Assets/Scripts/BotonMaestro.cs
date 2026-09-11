using UnityEngine;
using TMPro;

/// <summary>
/// Máquina de modo global de SceneSimulador: Calibración ↔ RCP. Ya NO vive en un
/// botón físico (los cubos Boton_Start_RCP / Boton_Selector_Movimiento se
/// eliminaron por diseño); este componente vive en un GO persistente (GestorRCP)
/// y su <see cref="Start"/> deja la escena en modo Calibración. El cambio de modo
/// se dispara desde el ítem "Cambiar de modo" del menú de pausa
/// (<see cref="AlternarModo"/>, con etiqueta dinámica vía <see cref="IEtiquetaDinamica"/>).
/// </summary>
public class BotonMaestro : MonoBehaviour, IEtiquetaDinamica
{
    [Header("Scripts a Controlar")]
    public CalibradorPosicion scriptCalibrador; // El que mueve el cuerpo
    public LogicaRCP scriptJuego;               // El que hace la simulación

    [Header("Visuales (opcional — hoy sin botón físico)")]
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

    /// <summary>Alterna Configuración/RCP. Lo dispara el ítem "Cambiar de modo"
    /// del menú de pausa (GestorMenuPausa).</summary>
    public void AlternarModo()
    {
        enModoConfiguracion = !enModoConfiguracion;
        AplicarEstado();
        Debug.Log("Cambio de Modo Global: " + (enModoConfiguracion ? "CONFIGURACIÓN" : "RCP"));
    }

    /// <summary>IEtiquetaDinamica: etiqueta del ítem de GestorMenuPausa, según
    /// el modo al que se pasaría si se selecciona.</summary>
    public string ObtenerEtiqueta() =>
        enModoConfiguracion ? "Cambiar a modo Simulación" : "Cambiar a modo Calibración";

    void AplicarEstado()
    {
        // 1. CONFIGURAMOS EL CALIBRADOR
        if (scriptCalibrador != null)
            scriptCalibrador.SetSistemaActivo(enModoConfiguracion);

        // 2. CONFIGURAMOS LA LÓGICA RCP
        if (scriptJuego != null)
        {
            scriptJuego.modoCalibracion = enModoConfiguracion;

            // FIX 4: Limpiar estado interno en cada transición de modo.
            // Sin esto, enPosicionCorrecta y estaEmpujando pueden quedar en true
            // de la sesión anterior, corrompiendo los cálculos de la nueva sesión.
            scriptJuego.ResetearEstadoSimulacion();
        }

        // 3. CAMBIAMOS EL COLOR DEL BOTÓN
        if (miRenderer != null)
            miRenderer.material.color = enModoConfiguracion ? colorModoConfig : colorModoJuego;

        // 4. TEXTO OPCIONAL
        if (textoEstado != null)
        {
            textoEstado.text  = enModoConfiguracion ? "Estado: CONFIGURACIÓN" : "Estado: RCP ACTIVO";
            textoEstado.color = enModoConfiguracion ? colorModoConfig : colorModoJuego;
        }

        // NOTA: el ícono de pausa ya NO se gatea por modo. Ahora es el único
        // acceso al cambio de modo, así que debe estar disponible siempre
        // (GestorMenuPausa.pausaDisponible = true en la escena).
    }
}
