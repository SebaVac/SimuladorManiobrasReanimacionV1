#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fondo de box para SceneInicio (2026-11-19), reutilizando lo ya construido en SceneDiagnostico. Crea el root
/// <c>EntornoInicio</c> con COPIAS de las piezas de EntornoBoxConsulta (suelo, techo, paredes, mobiliario, luces) y de
/// AmbientacionUrgencias (luminarias, esquinas, equipo de la puerta, letrero de la puerta), SIN camilla ni paciente ni
/// scripts de otros módulos (LucesBoxConsulta, RegistroAmbientacion, AmbienteSonoroUrgencias) y sin sonido.
///
/// SceneDiagnostico se abre en modo aditivo SOLO PARA LEER y se cierra sin guardar: sus objetos no se reparentan ni se
/// modifican (su MD5 no cambia). Los materiales son los mismos assets (Mat_Amb_*, material del pack); las mallas
/// fusionadas de la ambientación viven dentro de la escena y se clonan (Object.Instantiate) para que la copia no
/// referencie otra escena. Todo queda Static (los letreros TMP, sin BatchingStatic porque TMP reescribe su malla) y sin
/// colliders. Luces: las 3 de Diagnóstico (1 direccional + 2 puntuales, tiempo real, sin sombras).
///
/// También: ambiente plano igual al de Diagnóstico, PuntoInicioJugador en (0,0,0) yaw 0 y AlineadorInicioJugador en el
/// OVRCameraRig (mismos pasos que ConfigurarInicioJugador, que rechaza SceneInicio; espera de tracking de 1,5 s).
///
/// Idempotente: borra y recrea <c>EntornoInicio</c> en cada ejecución; el marcador y el alineador se reutilizan si ya
/// existen. Aborta si la escena activa no es SceneInicio. NO ejecuta ConstruirBoxConsultaDiagnostico,
/// ConstruirArranqueSeccion2 ni ConstruirSalaRCP y NO toca los URP.
/// Menú: Tools/Inicio del jugador/Construir fondo de box (SceneInicio)
/// </summary>
public static class ConstruirFondoInicio
{
    public const string RAIZ = "EntornoInicio";
    const string RUTA_DIAGNOSTICO = "Assets/Scenes/SceneDiagnostico.unity";
    const string NOMBRE_MARCADOR = "PuntoInicioJugador";

    static readonly StaticEditorFlags FLAGS_ESTATICOS =
        StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;
    static readonly StaticEditorFlags FLAGS_TEXTO =
        StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    // Piezas copiadas: (root en Diagnóstico, hijo). Lo que NO se copia: Camilla (depende del paciente/PosePaciente),
    // PanelCabecera / Letrero_Cabecera / PosteSuero / Cortina_Derecha (ambientan la zona de la camilla) y Sonido.
    static readonly string[] GRUPOS_ENTORNO = { "Suelo", "Techo", "Paredes", "Mobiliario", "Luces" };
    static readonly string[] PIEZAS_AMBIENTACION = { "Provisional/Luminarias", "Provisional/EquipoPuerta_GelYCortopunzantes", "Esquinas", "Letreros/Letrero_Puerta" };

    [MenuItem("Tools/Inicio del jugador/Construir fondo de box (SceneInicio)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneInicio")
        {
            Debug.LogError("[FondoInicio] La escena activa es '" + escena.name + "'. Abre SceneInicio: este builder no se ejecuta en otras escenas.");
            return;
        }
        var rig = Object.FindFirstObjectByType<OVRCameraRig>();
        if (rig == null) { Debug.LogError("[FondoInicio] No hay OVRCameraRig en SceneInicio."); return; }

        // 0. Idempotencia
        var previo = GameObject.Find(RAIZ);
        if (previo != null) Object.DestroyImmediate(previo);

        // 1. Leer SceneDiagnostico (aditivo, sin guardar)
        var escDiag = EditorSceneManager.OpenScene(RUTA_DIAGNOSTICO, OpenSceneMode.Additive);
        EditorSceneManager.SetActiveScene(escena);
        Color colorAmbiente = new Color(0.60f, 0.62f, 0.66f);
        var raiz = new GameObject(RAIZ);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        int piezas = 0, faltan = 0;
        try
        {
            var entornoDiag = BuscarRaiz(escDiag, "EntornoBoxConsulta");
            var ambDiag = BuscarRaiz(escDiag, "AmbientacionUrgencias");
            if (entornoDiag == null || ambDiag == null)
            {
                Debug.LogError("[FondoInicio] No encuentro EntornoBoxConsulta / AmbientacionUrgencias en SceneDiagnostico.");
                Object.DestroyImmediate(raiz);
                return;
            }
            var luces = entornoDiag.GetComponent<LucesBoxConsulta>();
            if (luces != null) colorAmbiente = new SerializedObject(luces).FindProperty("colorAmbiente").colorValue;

            foreach (var g in GRUPOS_ENTORNO)
            {
                var origen = entornoDiag.transform.Find(g);
                if (origen == null) { faltan++; Debug.LogWarning("[FondoInicio] Falta el grupo " + g); continue; }
                piezas += Copiar(origen.gameObject, raiz.transform, escena, g);
            }
            foreach (var ruta in PIEZAS_AMBIENTACION)
            {
                var origen = ambDiag.transform.Find(ruta);
                if (origen == null) { faltan++; Debug.LogWarning("[FondoInicio] Falta la pieza " + ruta); continue; }
                string nombreGrupo = ruta.Contains("/") ? ruta.Substring(0, ruta.IndexOf('/')) : "";
                Transform padre = raiz.transform;
                if (nombreGrupo.Length > 0)
                {
                    padre = raiz.transform.Find("Ambientacion_" + nombreGrupo);
                    if (padre == null) { var gg = new GameObject("Ambientacion_" + nombreGrupo); gg.transform.SetParent(raiz.transform, false); padre = gg.transform; }
                }
                piezas += Copiar(origen.gameObject, padre, escena, null);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(escDiag, true);   // sin guardar: SceneDiagnostico no se modifica
            EditorSceneManager.SetActiveScene(escena);
        }

        // 2. Todo estático, sin colliders, sin sombras en las piezas decorativas ya copiadas (se respetan las de origen)
        int colliders = 0;
        foreach (var c in raiz.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); colliders++; }
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            bool texto = t.GetComponent<TMPro.TMP_Text>() != null;
            bool tieneRenderer = t.GetComponent<Renderer>() != null;
            if (tieneRenderer || t.GetComponent<Light>() != null || t.childCount > 0)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, texto ? FLAGS_TEXTO : FLAGS_ESTATICOS);
        }

        // 3. Ambiente plano igual al de Diagnóstico; sin skybox
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = colorAmbiente;
        RenderSettings.skybox = null;
        RenderSettings.fog = false;

        // 4. Marcador de inicio y alineador (mismos pasos que ConfigurarInicioJugador)
        var marcador = Object.FindFirstObjectByType<PuntoInicioJugador>();
        if (marcador == null)
        {
            var go = new GameObject(NOMBRE_MARCADOR);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            marcador = go.AddComponent<PuntoInicioJugador>();
        }
        var ovr = rig.GetComponent<OVRManager>();
        if (ovr != null && ovr.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel) { ovr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel; EditorUtility.SetDirty(ovr); }
        var p = rig.transform.position;
        if (Mathf.Abs(p.y) > 1e-5f) rig.transform.position = new Vector3(p.x, 0f, p.z);
        var alineador = rig.GetComponent<AlineadorInicioJugador>();
        if (alineador == null) alineador = rig.gameObject.AddComponent<AlineadorInicioJugador>();
        var so = new SerializedObject(alineador);
        so.FindProperty("marcador").objectReferenceValue = marcador;
        so.FindProperty("cabeza").objectReferenceValue = rig.centerEyeAnchor;
        so.FindProperty("raizRig").objectReferenceValue = rig.transform;
        so.FindProperty("tiempoMaximoEsperaSeg").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(alineador);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[FondoInicio] " + piezas + " objetos copiados (faltantes: " + faltan + "), colliders quitados: " + colliders + ", ambiente " + colorAmbiente +
                  ", marcador en " + marcador.transform.position + ", alineador en " + rig.name + ". Escena guardada.");
    }

    static GameObject BuscarRaiz(Scene escena, string nombre)
    {
        foreach (var r in escena.GetRootGameObjects()) if (r.name == nombre) return r;
        return null;
    }

    /// <summary>Copia un objeto de SceneDiagnostico a la escena de Inicio bajo <paramref name="padre"/> (mismas poses locales).
    /// Las mallas que no son assets (las fusionadas de la ambientación) y los materiales que no son assets se clonan.</summary>
    static int Copiar(GameObject origen, Transform padre, Scene destino, string nombre)
    {
        var copia = Object.Instantiate(origen);
        SceneManager.MoveGameObjectToScene(copia, destino);
        copia.name = nombre ?? origen.name;
        copia.transform.SetParent(padre, false);
        copia.transform.localPosition = origen.transform.localPosition;
        copia.transform.localRotation = origen.transform.localRotation;
        copia.transform.localScale = origen.transform.localScale;

        // Camilla y compañía fuera, por si algún grupo la trae
        var quitar = new List<GameObject>();
        foreach (var t in copia.GetComponentsInChildren<Transform>(true)) if (t.name == "Camilla") quitar.Add(t.gameObject);
        foreach (var g in quitar) Object.DestroyImmediate(g);

        // Mallas y materiales no-asset → clones propios
        foreach (var mf in copia.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mf.sharedMesh))) mf.sharedMesh = Object.Instantiate(mf.sharedMesh);
        foreach (var r in copia.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; bool cambio = false;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(ms[i]))) { ms[i] = Object.Instantiate(ms[i]); cambio = true; }
            if (cambio) r.sharedMaterials = ms;
        }
        // Scripts de otros módulos: ninguno debe viajar (solo componentes de render/luz/texto)
        foreach (var mb in copia.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string ns = mb.GetType().Namespace ?? "";
            if (ns.StartsWith("TMPro") || ns.StartsWith("UnityEngine.Rendering")) continue;
            Debug.LogWarning("[FondoInicio] Se quita el script ajeno " + mb.GetType().Name + " de " + mb.name);
            Object.DestroyImmediate(mb);
        }
        return copia.GetComponentsInChildren<Transform>(true).Length;
    }
}
#endif
