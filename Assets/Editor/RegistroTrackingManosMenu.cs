#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Agrega o quita el registro temporal de tracking [TRACK] (root DEBUG_TRACKING_TEMPORAL) en SceneSimulador. Quitarlo borra el
/// root completo: ningún otro objeto lo referencia, así que no quedan referencias rotas. Aborta fuera de SceneSimulador.
/// </summary>
public static class RegistroTrackingManosMenu
{
    const string RAIZ = "DEBUG_TRACKING_TEMPORAL";

    [MenuItem("Tools/RCP/Agregar registro de tracking temporal (SceneSimulador)")]
    public static void Agregar()
    {
        var esc = EditorSceneManager.GetActiveScene();
        if (esc.name != "SceneSimulador") { Debug.LogError("[TRACK] Abre SceneSimulador."); return; }
        var previo = GameObject.Find(RAIZ); if (previo != null) Object.DestroyImmediate(previo);
        var go = new GameObject(RAIZ); var c = go.AddComponent<RegistroTrackingManos>(); var so = new SerializedObject(c);
        var logica = Object.FindFirstObjectByType<LogicaRCP>(); var cal = Object.FindFirstObjectByType<CalibradorPosicion>();
        so.FindProperty("logica").objectReferenceValue = logica;
        so.FindProperty("sesion").objectReferenceValue = Object.FindFirstObjectByType<SesionRCP>();
        so.FindProperty("cabeza").objectReferenceValue = cal != null ? cal.referenciaCamara : null;
        so.FindProperty("esternon").objectReferenceValue = logica != null ? logica.pechoVisual : null;
        so.FindProperty("manoIzquierda").objectReferenceValue = cal != null && cal.manoIzquierda != null ? cal.manoIzquierda.GetComponent<OVRHand>() : null;
        so.FindProperty("manoDerecha").objectReferenceValue = cal != null && cal.manoDerecha != null ? cal.manoDerecha.GetComponent<OVRHand>() : null;
        so.FindProperty("esqueletoIzquierdo").objectReferenceValue = cal != null && cal.manoIzquierda != null ? cal.manoIzquierda.GetComponent<OVRSkeleton>() : null;
        so.FindProperty("esqueletoDerecho").objectReferenceValue = cal != null && cal.manoDerecha != null ? cal.manoDerecha.GetComponent<OVRSkeleton>() : null;
        var p = so.FindProperty("paneles"); var lista = new System.Collections.Generic.List<Transform>();
        foreach (var n in new[] { "Panel_MetricasRCP", "Panel_GuiaManos" }) { var g = GameObject.Find(n); if (g != null) lista.Add(g.transform); }
        p.arraySize = lista.Count; for (int i = 0; i < lista.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = lista[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(esc); EditorSceneManager.SaveScene(esc);
        Debug.Log("[TRACK] Registro agregado y escena guardada.");
    }

    [MenuItem("Tools/RCP/Quitar registro de tracking temporal (SceneSimulador)")]
    public static void Quitar()
    {
        var esc = EditorSceneManager.GetActiveScene();
        if (esc.name != "SceneSimulador") { Debug.LogError("[TRACK] Abre SceneSimulador."); return; }
        var g = GameObject.Find(RAIZ);
        if (g == null) { Debug.Log("[TRACK] No hay registro que quitar."); return; }
        Object.DestroyImmediate(g);
        EditorSceneManager.MarkSceneDirty(esc); EditorSceneManager.SaveScene(esc);
        Debug.Log("[TRACK] Registro quitado y escena guardada.");
    }
}
#endif
