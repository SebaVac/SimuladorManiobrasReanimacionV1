using UnityEngine;

/// <summary>
/// Iluminación del box de consulta de SceneDiagnostico: una luz principal direccional y dos puntuales de techo
/// (3 luces en total, tiempo real, SIN sombras) más el ambiente plano de RenderSettings. Las intensidades y colores
/// son campos serializados fáciles de ajustar; se aplican al arrancar y al editarlos en el Inspector. Sin
/// asignaciones por fotograma (no tiene Update).
/// </summary>
[DisallowMultipleComponent]
public class LucesBoxConsulta : MonoBehaviour
{
    [Header("Luces (tiempo real, sin sombras)")]
    [SerializeField] private Light luzPrincipal;
    [SerializeField] private Light[] puntualesTecho;
    [SerializeField] private float intensidadPrincipal = 0.8f;
    [SerializeField] private float intensidadPuntual = 0.9f;
    [SerializeField] private float rangoPuntual = 5.5f;
    [SerializeField] private Color colorLuz = new Color(1f, 0.96f, 0.90f);

    [Header("Ambiente (RenderSettings, plano)")]
    [SerializeField] private Color colorAmbiente = new Color(0.52f, 0.52f, 0.54f);

    void Awake() => Aplicar();

    public void Aplicar()
    {
        if (luzPrincipal != null) { luzPrincipal.intensity = intensidadPrincipal; luzPrincipal.color = colorLuz; luzPrincipal.shadows = LightShadows.None; }
        if (puntualesTecho != null)
            for (int i = 0; i < puntualesTecho.Length; i++)
                if (puntualesTecho[i] != null) { puntualesTecho[i].intensity = intensidadPuntual; puntualesTecho[i].range = rangoPuntual; puntualesTecho[i].color = colorLuz; puntualesTecho[i].shadows = LightShadows.None; }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = colorAmbiente;
    }

#if UNITY_EDITOR
    void OnValidate() { if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Aplicar(); }; }
#endif
}
