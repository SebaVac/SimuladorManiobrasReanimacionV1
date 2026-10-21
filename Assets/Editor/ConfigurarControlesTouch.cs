#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Añade el soporte de controles Touch a la escena activa: SceneInicio o SceneDiagnostico (aborta en
/// SceneSimulador, que sigue solo con manos). Idempotente: destruye y recrea lo que genera.
///   - Modelos de controles (OVRControllerPrefab del SDK) bajo las anclas de control del OVRCameraRig.
///   - <c>EntradaControles</c> (EntradaControlesTouch) con las anclas, las manos y los visuales a conmutar.
///   - <c>VisualControles</c> (VisualControlesTouch): línea teal, retículo y esfera de punta por control.
///   - GestorSeleccionToque.permitirControles = true; en SceneDiagnostico además InteraccionPaciente
///     (punta virtual) y GestorCasoClinico (dispositivo de entrada para el Debriefing).
/// No toca el AndroidManifest ni OVRProjectConfig (handTrackingSupport = ControllersAndHands ya está).
/// No ejecuta ConstruirArranqueSeccion2.
/// Menú: Tools/Controles Touch/Configurar escena actual
/// </summary>
public static class ConfigurarControlesTouch
{
    const string RUTA_PREFAB_CONTROL = "Packages/com.meta.xr.sdk.core/Prefabs/OVRControllerPrefab.prefab";
    const string RUTA_MATERIALES = "Assets/Controles";
    static readonly Color TEAL = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Controles Touch/Configurar escena actual")]
    public static void Configurar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneInicio" && escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena no admitida",
                "Los controles Touch solo se añaden a SceneInicio y SceneDiagnostico.\nActual: " + escena.name, "OK");
            return;
        }

        var rig = GameObject.Find("OVRCameraRig");
        var arbitro = Object.FindFirstObjectByType<GestorSeleccionToque>();
        if (rig == null || arbitro == null) { Debug.LogError("[ControlesTouch] Falta OVRCameraRig o GestorSeleccionToque."); return; }

        Limpiar("EntradaControles", "VisualControles");

        var ancIzq = rig.transform.Find("TrackingSpace/LeftHandAnchor/LeftControllerAnchor");
        var ancDer = rig.transform.Find("TrackingSpace/RightHandAnchor/RightControllerAnchor");
        if (ancIzq == null || ancDer == null) { Debug.LogError("[ControlesTouch] Faltan las anclas de control del rig."); return; }

        // ── Modelos de controles ──────────────────────────────────────────
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB_CONTROL);
        if (prefab == null) { Debug.LogError("[ControlesTouch] No se encontró " + RUTA_PREFAB_CONTROL); return; }
        var modelos = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            var anc = i == 0 ? ancIzq : ancDer;
            var viejo = anc.Find("OVRControllerPrefab_" + (i == 0 ? "L" : "R"));
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anc);
            inst.name = "OVRControllerPrefab_" + (i == 0 ? "L" : "R");
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var h in inst.GetComponentsInChildren<OVRControllerHelper>(true))
            {
                var so = new SerializedObject(h);
                so.FindProperty("m_controller").intValue = (int)(i == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            modelos[i] = inst;
        }

        // ── Manos del rig ────────────────────────────────────────────────
        OVRHand manoIzq = null, manoDer = null;
        var comportamientos = new List<Behaviour>(); var mallas = new List<Renderer>();
        foreach (var h in rig.GetComponentsInChildren<OVRHand>(true))
        {
            if (h.GetHand() == OVRPlugin.Hand.HandLeft) manoIzq = h; else manoDer = h;
            foreach (var r in h.GetComponents<OVRMeshRenderer>()) comportamientos.Add(r);
            foreach (var r in h.GetComponents<OVRSkeletonRenderer>()) comportamientos.Add(r);
            foreach (var r in h.GetComponents<SkinnedMeshRenderer>()) mallas.Add(r);
        }

        // ── EntradaControles ─────────────────────────────────────────────
        var goEntrada = new GameObject("EntradaControles");
        var entrada = goEntrada.AddComponent<EntradaControlesTouch>();
        Set(entrada, "anclaControlIzquierdo", ancIzq);
        Set(entrada, "anclaControlDerecho", ancDer);
        Set(entrada, "manoIzquierda", manoIzq);
        Set(entrada, "manoDerecha", manoDer);
        SetArray(entrada, "modelosControl", modelos);
        SetArray(entrada, "renderersMano", comportamientos.ToArray());
        SetArray(entrada, "mallasMano", mallas.ToArray());

        // ── Visuales ─────────────────────────────────────────────────────
        var mat = MaterialTeal();
        var goVis = new GameObject("VisualControles");
        var vis = goVis.AddComponent<VisualControlesTouch>();
        var lineas = new Object[2]; var reticulos = new Object[2]; var puntas = new Object[2];
        for (int i = 0; i < 2; i++)
        {
            string lado = i == 0 ? "L" : "R";
            var gl = new GameObject("Rayo_" + lado); gl.transform.SetParent(goVis.transform, false);
            var lr = gl.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.useWorldSpace = true; lr.widthMultiplier = 0.004f; lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View; lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.startColor = TEAL; lr.endColor = TEAL;
            gl.SetActive(false); lineas[i] = lr;

            reticulos[i] = Esfera("Reticulo_" + lado, goVis.transform, 0.012f, mat);
            puntas[i] = Esfera("PuntaVirtual_" + lado, goVis.transform, 0.02f, mat);
        }
        Set(vis, "entrada", entrada);
        Set(vis, "arbitro", arbitro);
        SetArray(vis, "lineas", lineas);
        SetArray(vis, "reticulos", reticulos);
        SetArray(vis, "puntas", puntas);

        // ── Cableado de consumidores ─────────────────────────────────────
        SetBool(arbitro, "permitirControles", true);
        Set(arbitro, "entradaControles", entrada);
        if (escena.name == "SceneDiagnostico")
        {
            var ip = Object.FindFirstObjectByType<InteraccionPaciente>();
            if (ip != null) Set(ip, "entradaControles", entrada); else Debug.LogError("[ControlesTouch] Falta InteraccionPaciente.");
            var gc = Object.FindFirstObjectByType<GestorCasoClinico>();
            if (gc != null) Set(gc, "entradaDispositivo", entrada); else Debug.LogError("[ControlesTouch] Falta GestorCasoClinico.");
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[ControlesTouch] Controles Touch configurados y guardados en " + escena.name + ".");
    }

    /// <summary>SceneSimulador debe quedar SIN controles: fija permitirControles = false explícitamente.</summary>
    [MenuItem("Tools/Controles Touch/Fijar SceneSimulador sin controles")]
    public static void FijarSimulador()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneSimulador") { Debug.LogError("[ControlesTouch] Abre SceneSimulador."); return; }
        var arbitro = Object.FindFirstObjectByType<GestorSeleccionToque>();
        if (arbitro == null) return;
        SetBool(arbitro, "permitirControles", false);
        Set(arbitro, "entradaControles", null);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    static Material MaterialTeal()
    {
        if (!AssetDatabase.IsValidFolder(RUTA_MATERIALES)) AssetDatabase.CreateFolder("Assets", "Controles");
        string ruta = RUTA_MATERIALES + "/ControlesTeal.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, ruta);
        }
        mat.SetColor("_BaseColor", TEAL);
        mat.color = TEAL;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Transform Esfera(string nombre, Transform padre, float diametro, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localScale = Vector3.one * diametro;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    static void Limpiar(params string[] nombres)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (var go in escena.GetRootGameObjects())
            foreach (var n in nombres)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ControlesTouch] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object comp, string campo, bool valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ControlesTouch] Campo no encontrado: " + campo); return; }
        p.boolValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object comp, string campo, Object[] valores)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ControlesTouch] Campo no encontrado: " + campo); return; }
        p.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
