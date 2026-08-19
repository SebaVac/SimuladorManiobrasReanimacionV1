using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipal : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelPrincipal;
    public GameObject panelSubMenuRCP;
    public GameObject panelSubMenuModulos;
    public GameObject panelTutorialRCP;

    [Header("Nombres de escena")]
    public string escenaRCP     = "SampleScene";
    public string escenaModulos = "SampleScene";

    void Start()
    {
        MostrarPrincipal();
    }

    // ── Menú principal ─────────────────────────────────────────────────────

    public void SeleccionarModoRCP()
    {
        panelPrincipal.SetActive(false);
        panelSubMenuRCP.SetActive(true);
        panelSubMenuModulos.SetActive(false);
    }

    public void SeleccionarModulos()
    {
        panelPrincipal.SetActive(false);
        panelSubMenuRCP.SetActive(false);
        panelSubMenuModulos.SetActive(true);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Submenú RCP ────────────────────────────────────────────────────────

    public void IniciarRCP()
    {
        SceneManager.LoadScene(escenaRCP);
    }

    public void AbrirTutorialRCP()
    {
        panelSubMenuRCP.SetActive(false);
        if (panelTutorialRCP != null) panelTutorialRCP.SetActive(true);
    }

    public void CerrarTutorialRCP()
    {
        if (panelTutorialRCP != null) panelTutorialRCP.SetActive(false);
        panelSubMenuRCP.SetActive(true);
    }

    public void VolverDesdeRCP()
    {
        MostrarPrincipal();
    }

    // ── Submenú Módulos ────────────────────────────────────────────────────

    public void IniciarModulos()
    {
        SceneManager.LoadScene(escenaModulos);
    }

    public void AbrirTutorialModulos()
    {
        Debug.Log("Tutorial Módulos — pendiente de implementar");
    }

    public void VolverDesdeModulos()
    {
        MostrarPrincipal();
    }

    // ── Helper ─────────────────────────────────────────────────────────────

    void MostrarPrincipal()
    {
        panelPrincipal.SetActive(true);
        panelSubMenuRCP.SetActive(false);
        panelSubMenuModulos.SetActive(false);
    }
}
