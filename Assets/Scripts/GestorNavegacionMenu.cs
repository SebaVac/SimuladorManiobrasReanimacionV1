using UnityEngine;
using UnityEngine.SceneManagement;

public class GestorNavegacionMenu : MonoBehaviour
{
    // Fuente única de verdad del nombre de la escena del menú principal.
    // const (no de instancia): GestorMenuPausa vive en SceneSimulador y
    // SceneDiagnostico, escenas que nunca tienen cargada a la vez una
    // instancia de GestorNavegacionMenu (que solo existe en SceneInicio) —
    // una referencia de Inspector cruzada entre escenas no es posible en
    // Unity, así que el acceso es estático: GestorNavegacionMenu.escenaMenuPrincipal.
    public const string escenaMenuPrincipal = "SceneInicio";

    [Header("Escenas")]
    public string escenaSimuladorRCP = "SceneSimulador";
    public string escenaRazonamientoClinico = "SceneDiagnostico";

    public void IniciarSimulacionRCP()
    {
        SceneManager.LoadScene(escenaSimuladorRCP);
    }

    // Usado por el botón "Volver al menú principal" del Módulo de Razonamiento Clínico.
    public void VolverAlMenuPrincipal()
    {
        SceneManager.LoadScene(escenaMenuPrincipal);
    }

    public void IrARazonamientoClinico()
    {
        if (string.IsNullOrEmpty(escenaRazonamientoClinico))
        {
            Debug.Log("Módulo de Razonamiento Clínico aún no implementado — falta conectar la escena.");
            return;
        }
        SceneManager.LoadScene(escenaRazonamientoClinico);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
