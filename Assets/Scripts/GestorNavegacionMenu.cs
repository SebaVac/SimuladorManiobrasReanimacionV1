using UnityEngine;
using UnityEngine.SceneManagement;

public class GestorNavegacionMenu : MonoBehaviour
{
    [Header("Escenas")]
    public string escenaSimuladorRCP = "SceneSimulador";
    public string escenaRazonamientoClinico = "";

    public void IniciarSimulacionRCP()
    {
        SceneManager.LoadScene(escenaSimuladorRCP);
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
