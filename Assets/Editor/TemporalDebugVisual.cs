using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// TEMPORAL — instala y quita las herramientas de diagnóstico bajo el root "DEBUG_VISUAL_TEMPORAL"
/// (DiagnosticoVisualTemporal y, en SceneSimulador, DiagnosticoMetronomoTemporal) y sus ítems del menú de pausa.
/// Quitar borra el root y, ANTES, los ítems del menú de pausa que apuntan a componentes del root, para no dejar
/// referencias rotas.
/// </summary>
public static class TemporalDebugVisual
{
    public const string RAIZ = "DEBUG_VISUAL_TEMPORAL";
    private const string RUTA_ESC_DIAG = "Assets/Scenes/SceneDiagnostico.unity";
    private const string RUTA_ESC_SIM = "Assets/Scenes/SceneSimulador.unity";
    private const string RUTA_RENDERER = "Assets/Settings/URP-Balanced-Renderer.asset";

    // ── Menús ───────────────────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Temporal/Instalar diagnóstico visual (escena actual)")]
    public static void InstalarVisualEscenaActual()
    {
        string msg = InstalarVisual(SceneManager.GetActiveScene());
        Debug.Log("[DIAGVIS] " + msg);
    }

    [MenuItem("Tools/Temporal/Instalar ítems de metrónomo (SceneSimulador, escena actual)")]
    public static void InstalarMetronomoEscenaActual()
    {
        Debug.Log("[METRO] " + InstalarMetronomo(SceneManager.GetActiveScene()));
    }

    [MenuItem("Tools/Temporal/Quitar DEBUG_VISUAL_TEMPORAL (SceneDiagnostico y SceneSimulador)")]
    public static void QuitarEnAmbasEscenas()
    {
        string anterior = SceneManager.GetActiveScene().path;
        foreach (var ruta in new[] { RUTA_ESC_DIAG, RUTA_ESC_SIM })
        {
            var sc = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            string r = Quitar(sc);
            EditorSceneManager.SaveScene(sc);
            Debug.Log("[DIAGVIS] " + sc.name + ": " + r);
        }
        if (!string.IsNullOrEmpty(anterior)) EditorSceneManager.OpenScene(anterior, OpenSceneMode.Single);
    }

    // ── Instalar ────────────────────────────────────────────────────────────────────────────────────

    public static string InstalarVisual(Scene sc)
    {
        bool esSim = sc.name == "SceneSimulador";
        if (!esSim && sc.name != "SceneDiagnostico") return "solo para SceneDiagnostico / SceneSimulador";

        var raiz = ObtenerRaiz(sc);
        var existente = raiz.GetComponentInChildren<DiagnosticoVisualTemporal>(true);
        if (existente != null) return "ya instalado";

        var go = new GameObject("DiagnosticoVisualTemporal");
        go.transform.SetParent(raiz.transform, false);
        var comp = go.AddComponent<DiagnosticoVisualTemporal>();

        var gestor = Object.FindFirstObjectByType<GestorMenuPausa>(FindObjectsInactive.Include);
        var soG = new SerializedObject(gestor);
        var panel = soG.FindProperty("panelPausa").objectReferenceValue as GameObject;

        var texto = CrearTexto(go.transform);

        var so = new SerializedObject(comp);
        so.FindProperty("raizEntorno").objectReferenceValue = BuscarRaiz(sc, esSim ? "EntornoSalaRCP" : "EntornoBoxConsulta")?.transform;
        so.FindProperty("raizAmbientacion").objectReferenceValue = BuscarRaiz(sc, "AmbientacionUrgencias")?.transform;
        so.FindProperty("salaVieja").objectReferenceValue = esSim ? BuscarRaiz(sc, "EntornoSalaUrgencias") : null;
        so.FindProperty("raizPaciente").objectReferenceValue = BuscarRaiz(sc, "Paciente_Completo")?.transform;
        so.FindProperty("rendererData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RUTA_RENDERER);
        so.FindProperty("globalVolume").objectReferenceValue = esSim ? Object.FindFirstObjectByType<Volume>() : null;
        so.FindProperty("texto").objectReferenceValue = texto;
        so.FindProperty("panelPausa").objectReferenceValue = panel != null ? panel.GetComponent<RectTransform>() : null;
        so.FindProperty("itemsEnMenu").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        AgregarItemPausa(gestor, "Diag visual: siguiente (0/" + DiagnosticoVisualTemporal.ULTIMO_ESTADO + ")", comp, comp, nameof(DiagnosticoVisualTemporal.Siguiente), true);

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        return "instalado en " + sc.name;
    }

    public static string InstalarMetronomo(Scene sc)
    {
        if (sc.name != "SceneSimulador") return "solo para SceneSimulador";
        var raiz = ObtenerRaiz(sc);
        if (raiz.GetComponentInChildren<DiagnosticoMetronomoTemporal>(true) != null) return "ya instalado";

        var gestor = Object.FindFirstObjectByType<GestorMenuPausa>(FindObjectsInactive.Include);
        var panel = new SerializedObject(gestor).FindProperty("panelPausa").objectReferenceValue as GameObject;
        var metronomo = Object.FindFirstObjectByType<MetronomoRCP>(FindObjectsInactive.Include);
        var ambiente = Object.FindFirstObjectByType<AmbienteSonoroUrgencias>(FindObjectsInactive.Include);

        string[] nombres = { "Metro_Modo", "Metro_ClickPrueba", "Metro_DspBuffer" };
        string[] etiquetas = { "Metro audio: modo 1 por tic", "Metro: click de prueba", "Metro: DSP buffer 256" };
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(nombres[i]);
            go.transform.SetParent(raiz.transform, false);
            var comp = go.AddComponent<DiagnosticoMetronomoTemporal>();
            var so = new SerializedObject(comp);
            so.FindProperty("accion").enumValueIndex = i;
            so.FindProperty("metronomo").objectReferenceValue = metronomo;
            so.FindProperty("ambiente").objectReferenceValue = ambiente;
            so.FindProperty("panelPausa").objectReferenceValue = panel != null ? panel.GetComponent<RectTransform>() : null;
            so.FindProperty("itemsQueAmplia").intValue = i == 0 ? 3 : 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            AgregarItemPausa(gestor, etiquetas[i], comp, comp, nameof(DiagnosticoMetronomoTemporal.Ejecutar), true);
        }
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        return "instalados 3 ítems de metrónomo en " + sc.name;
    }

    private static TextMeshPro CrearTexto(Transform padre)
    {
        var t = new GameObject("TextoEstado");
        t.transform.SetParent(padre, false);
        var tmp = t.AddComponent<TextMeshPro>();
        tmp.text = "DIAGVIS 0/" + DiagnosticoVisualTemporal.ULTIMO_ESTADO + "\nnormal";
        tmp.fontSize = 0.16f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.25f);
        tmp.enableWordWrapping = false;
        tmp.rectTransform.sizeDelta = new Vector2(0.5f, 0.07f);
        return tmp;
    }

    // ── Quitar ──────────────────────────────────────────────────────────────────────────────────────

    public static string Quitar(Scene sc)
    {
        GameObject raiz = null;
        foreach (var g in sc.GetRootGameObjects()) if (g.name == RAIZ) raiz = g;
        if (raiz == null) return "no hay " + RAIZ;

        // 1) ítems del menú de pausa que apuntan a algo del root
        int quitados = 0;
        foreach (var gestor in Object.FindObjectsByType<GestorMenuPausa>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(gestor);
            var items = so.FindProperty("items");
            for (int i = items.arraySize - 1; i >= 0; i--)
            {
                if (!ItemApuntaA(items.GetArrayElementAtIndex(i), raiz.transform)) continue;
                items.DeleteArrayElementAtIndex(i); quitados++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // 2) el root
        Object.DestroyImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(sc);
        return "quitado " + RAIZ + " y " + quitados + " ítem(s) del menú de pausa";
    }

    private static bool ItemApuntaA(SerializedProperty item, Transform raiz)
    {
        foreach (var nombre in new[] { "proveedorEtiqueta", "proveedorCondicion" })
        {
            var c = item.FindPropertyRelative(nombre).objectReferenceValue as Component;
            if (c != null && c.transform.IsChildOf(raiz)) return true;
        }
        var calls = item.FindPropertyRelative("accion.m_PersistentCalls.m_Calls");
        for (int k = 0; k < calls.arraySize; k++)
        {
            var c = calls.GetArrayElementAtIndex(k).FindPropertyRelative("m_Target").objectReferenceValue as Component;
            if (c != null && c.transform.IsChildOf(raiz)) return true;
        }
        return false;
    }

    // ── Utilidades (también las usa DiagnosticoMetronomoTemporal) ────────────────────────────────────

    public static GameObject ObtenerRaiz(Scene sc)
    {
        foreach (var g in sc.GetRootGameObjects()) if (g.name == RAIZ) return g;
        var n = new GameObject(RAIZ);
        SceneManager.MoveGameObjectToScene(n, sc);
        return n;
    }

    public static GameObject BuscarRaiz(Scene sc, string nombre)
    {
        foreach (var g in sc.GetRootGameObjects()) if (g.name == nombre) return g;
        return null;
    }

    /// <summary>Añade un ítem al final de la lista del menú de pausa, con etiqueta dinámica, condición y una acción
    /// (método sin parámetros del mismo componente).</summary>
    public static void AgregarItemPausa(GestorMenuPausa gestor, string etiquetaFija, MonoBehaviour proveedor, MonoBehaviour objetivo, string metodo, bool mantenerAbierto)
    {
        var so = new SerializedObject(gestor);
        var items = so.FindProperty("items");
        int n = items.arraySize;
        items.arraySize = n + 1;
        var e = items.GetArrayElementAtIndex(n);
        e.FindPropertyRelative("etiqueta").stringValue = etiquetaFija;
        e.FindPropertyRelative("proveedorEtiqueta").objectReferenceValue = proveedor;
        e.FindPropertyRelative("proveedorCondicion").objectReferenceValue = proveedor;
        e.FindPropertyRelative("mantenerMenuAbierto").boolValue = mantenerAbierto;
        e.FindPropertyRelative("requiereConfirmacion").boolValue = false;
        e.FindPropertyRelative("tituloConfirmacion").stringValue = "";
        e.FindPropertyRelative("mensajeConfirmacion").stringValue = "";
        var calls = e.FindPropertyRelative("accion.m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var c = calls.GetArrayElementAtIndex(0);
        c.FindPropertyRelative("m_Target").objectReferenceValue = objetivo;
        c.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = objetivo.GetType().AssemblyQualifiedName;
        c.FindPropertyRelative("m_MethodName").stringValue = metodo;
        c.FindPropertyRelative("m_Mode").intValue = 1;      // Void
        c.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
