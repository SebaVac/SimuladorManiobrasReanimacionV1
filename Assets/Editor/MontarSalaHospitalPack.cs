#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú: Tools > Montar Sala Urgencias (Hospital Pack)
///
/// Construye la sala de urgencias VR con los FBX del pack Atomic Realm.
/// Constraints no negociables:
///   - globalScale de todos los FBX queda en 1 (sin corrección de escala).
///   - Ninguna cama del pack se usa bajo el maniquí: se coloca Plataforma_Entrenamiento
///     (Cube primitivo, top en Y=0.300 = alturaSuelo de CalibradorPosicion).
///   - No se agrega Rigidbody ni Collider a nada dentro del volumen de hand-tracking.
///   - No se toca Paciente_Completo, LogicaRCP.cs ni CalibradorPosicion.cs.
///   - overhead_light (Y=2.338 m) NO se instancia — no cabe en sala de 2 m.
/// </summary>
public static class MontarSalaHospitalPack
{
    const string ROOT_NAME    = "EntornoSalaUrgencias";
    const string PACK_FOLDER  = "Assets/Enviroment/HospitalAssets/Models/fbx (Unity Rotated)";
    const string TEXTURA_PATH = "Assets/Enviroment/HospitalAssets/Textures/hospital_gradient_uv.png";
    const string MATERIAL_PATH = "Assets/Enviroment/HospitalAssets/Materials/hospital_gradient_uv.mat";

    // ── Layout ─────────────────────────────────────────────────────────────────
    // Escala cosmética del entorno (room shell + mobiliario decorativo).
    // Plataforma_Entrenamiento y Paciente_Completo NO se ven afectados (ver InstanciarPack).
    const float ENV_SCALE = 2f;

    // Sala (ENV_SCALE=2): X[-6,+6] × Z[-2,+14] × Y[0,+4]  → 12 m × 16 m × 4 m
    // Maniquí runtime: (0.95, 0.3, 2.05)  [Paciente_Completo(0,0,1.025) + BananaMan offset(0.95,0,1.027)]
    // Plataforma top = Y 0.300 = CalibradorPosicion.alturaSuelo  ✓ (intacta)

    [MenuItem("Tools/Montar Sala Urgencias (Hospital Pack)")]
    static void Montar()
    {
        if (GameObject.Find(ROOT_NAME) != null)
        {
            EditorUtility.DisplayDialog("Sala ya existe",
                $"Ya existe '{ROOT_NAME}' en la escena.\n" +
                "Eliminalo antes de volver a ejecutar este script.", "OK");
            return;
        }

        var matPack = ObtenerMaterialPack();

        // ── ROOT ───────────────────────────────────────────────────────────────
        var root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "Montar Sala Hospital Pack");

        // ── GEOMETRÍA DE SALA ──────────────────────────────────────────────────
        var geoSala = Hijo(root, "Geometria_Sala");

        // SUELO — 3 cols × 4 filas de floor_1 escalado ENV_SCALE (tile efectivo: 4×4 m)
        //   Col X: -4, 0, +4    → cubre X[-6, +6]
        //   Fila Z:  0, 4, 8,12 → cubre Z[-2, +14]  (tile centrado en 0 abarca [-2,+2], etc.)
        var goSuelo = Hijo(geoSala, "Suelo");
        for (int xi = 0; xi < 3; xi++)
        for (int zi = 0; zi < 4; zi++)
            InstanciarPack("floor_1", goSuelo,
                new Vector3(-4f + xi * 4f, 0f, zi * 4f), Quaternion.identity, ENV_SCALE);

        // PAREDES — módulos de wall_1 escalados ENV_SCALE (módulo efectivo: 4×4 m)
        var goParedes = Hijo(geoSala, "Paredes");

        // Pared trasera  Z = -2
        for (int xi = 0; xi < 3; xi++)
            InstanciarPack("wall_1", goParedes,
                new Vector3(-4f + xi * 4f, 0f, -2f), Quaternion.identity, ENV_SCALE);

        // Pared frontal  Z = +14  (wall_door al centro)
        InstanciarPack("wall_1",    goParedes, new Vector3(-4f, 0f, 14f), Quaternion.Euler(0f, 180f, 0f), ENV_SCALE);
        InstanciarPack("wall_door", goParedes, new Vector3( 0f, 0f, 14f), Quaternion.Euler(0f, 180f, 0f), ENV_SCALE);
        InstanciarPack("wall_1",    goParedes, new Vector3( 4f, 0f, 14f), Quaternion.Euler(0f, 180f, 0f), ENV_SCALE);

        // Pared izquierda  X = -6  (rot 90° Y)
        for (int zi = 0; zi < 4; zi++)
            InstanciarPack("wall_1", goParedes,
                new Vector3(-6f, 0f, zi * 4f), Quaternion.Euler(0f, 90f, 0f), ENV_SCALE);

        // Pared derecha  X = +6  (rot 270° Y)  — módulo zi=2 (Z=8) con ventana
        for (int zi = 0; zi < 4; zi++)
        {
            string modelo = (zi == 2) ? "wall_window" : "wall_1";
            InstanciarPack(modelo, goParedes,
                new Vector3(6f, 0f, zi * 4f), Quaternion.Euler(0f, 270f, 0f), ENV_SCALE);
        }

        // TECHO — Quad primitivo (sin asset de techo en el pack)
        // ENV_SCALE=2 → sala 12 m × 16 m × 4 m → techo en Y=4, centro Z=6
        // MATERIAL: Mat_Sala_Techo (gris sólido) — NO el atlas UV del pack
        var goTecho = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Undo.RegisterCreatedObjectUndo(goTecho, "Techo");
        goTecho.name = "Techo";
        goTecho.transform.SetParent(geoSala.transform, false);
        goTecho.transform.position   = new Vector3(0f, 4f, 6f);
        goTecho.transform.rotation   = Quaternion.Euler(90f, 0f, 0f);
        goTecho.transform.localScale = new Vector3(12f, 16f, 1f);
        Object.DestroyImmediate(goTecho.GetComponent<MeshCollider>());
        goTecho.GetComponent<MeshRenderer>().sharedMaterial = CrearMatSolido(
            "Mat_Sala_Techo", new Color(0.88f, 0.88f, 0.86f));

        // LUZ — Point Light baked centrado en la sala ampliada
        // overhead_light FBX excluido (Y=2.338 m — no cabe en sala de 4 m sin escalar el asset)
        var goLuzGO = new GameObject("Luz_Sala_Point");
        Undo.RegisterCreatedObjectUndo(goLuzGO, "Luz Sala");
        goLuzGO.transform.SetParent(geoSala.transform, false);
        goLuzGO.transform.position = new Vector3(0f, 3.7f, 6f);
        var pLight = goLuzGO.AddComponent<Light>();
        pLight.type             = LightType.Point;
        pLight.range            = 18f;
        pLight.intensity        = 2.5f;
        pLight.color            = new Color(1f, 0.96f, 0.88f);
        pLight.lightmapBakeType = LightmapBakeType.Baked;
        pLight.shadows          = LightShadows.Soft;

        // ── PLATAFORMA ENTRENAMIENTO CPR ───────────────────────────────────────
        // RAZÓN DEL CAMBIO: el Cube primitivo tiene UVs estándar (0→1 por cara) que mapean
        // al atlas hospital_gradient_uv en coordenadas arbitrarias → "bloques de colores".
        // El atlas tiene regiones sólidas por zona UV; sólo los meshes del pack (diseñados
        // con esas UVs) muestran el color correcto. Solución: usar floor_1 FBX intacto.
        //
        // floor_1 bounds Y = 0.182 m. Asumiendo pivot en la BASE del tile (convención
        // estándar para arquitectura): pos.y = 0.300 - 0.182 = 0.118 → top = 0.300 ✓
        // Si el pivot NO está en la base (origen centrado): el top quedaría en 0.118+0.091
        // = 0.209 — ajustar Y en el Inspector hasta que la superficie coincida con Y=0.300.
        // addColliders: 0 en todos los .meta del pack → sin Collider automático ✓
        var goPlat = Hijo(root, "Plataforma_Entrenamiento");
        InstanciarPack("floor_1", goPlat, new Vector3(0.95f, 0.118f, 2.05f), Quaternion.identity, scale: 1f);

        // ── EQUIPAMIENTO ─────────────────────────────────────────────────────
        // Todas las piezas escaladas ENV_SCALE. Posiciones calculadas para mantener
        // el equipo alrededor del maniquí (0.95, 0.3, 2.05) en la sala ampliada.
        // La plataforma NO pertenece a este grupo → no hereda nada de aquí.
        var goEquipo = Hijo(root, "Equipamiento");

        // Desfibrilador / monitor (machine_1 × 2) — izquierda del maniquí
        // bounds original X=0.638 → 2× = 1.276 → separación visual mayor
        InstanciarPack("machine_1", goEquipo,
            new Vector3(-1.2f, 0f, 2.05f), Quaternion.Euler(0f, 90f, 0f), ENV_SCALE);

        // Carro equipo médico (machine_2 × 2) — derecha del maniquí
        InstanciarPack("machine_2", goEquipo,
            new Vector3(3.2f, 0f, 2.05f), Quaternion.Euler(0f, 270f, 0f), ENV_SCALE);

        // Mesa de instrumental (tray_1 × 2) + bandeja (tray_2 × 2) — a los pies
        // tray_1 top escalado = 0.568 × ENV_SCALE = 1.136 m
        InstanciarPack("tray_1", goEquipo,
            new Vector3(0.95f, 0f, 4.8f), Quaternion.identity, ENV_SCALE);
        InstanciarPack("tray_2", goEquipo,
            new Vector3(0.95f, 1.136f, 4.8f), Quaternion.identity, ENV_SCALE);

        // Armarios (inferior + superior × 2) — contra pared izquierda (ahora en X=-6)
        // cupboard_bottom top escalado = 0.689 × ENV_SCALE = 1.378 m
        float[] armZ = { 1.5f, 5.5f };
        foreach (float az in armZ)
        {
            InstanciarPack("cupboard_bottom", goEquipo,
                new Vector3(-5.0f, 0f, az), Quaternion.Euler(0f, 90f, 0f), ENV_SCALE);
            InstanciarPack("cupboard_top", goEquipo,
                new Vector3(-5.0f, 1.378f, az), Quaternion.Euler(0f, 90f, 0f), ENV_SCALE);
        }

        // ── DESHABILITAR PASSTHROUGH (modo VR puro) ────────────────────────────
        var passthrough = Object.FindObjectOfType<OVRPassthroughLayer>();
        if (passthrough != null)
        {
            Undo.RecordObject(passthrough.gameObject, "Disable Passthrough");
            passthrough.gameObject.SetActive(false);
        }

        // ── DIRECTIONAL LIGHT → BAKED ──────────────────────────────────────────
        foreach (var light in Object.FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                Undo.RecordObject(light, "DirLight Baked");
                light.lightmapBakeType = LightmapBakeType.Baked;
            }
        }

        // ── CÁMARAS → FONDO NEGRO ─────────────────────────────────────────────
        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            Undo.RecordObject(cam, "Camera BG Black");
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        // ── MARCAR STATICS ─────────────────────────────────────────────────────
        // ContributeGI + OccluderStatic + OccludeeStatic + BatchingStatic
        // No aplicar a objetos dentro del área de hand-tracking (el maniquí NO está bajo este root).
        const StaticEditorFlags STATIC_FLAGS =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic;

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            GameObjectUtility.SetStaticEditorFlags(mr.gameObject, STATIC_FLAGS);

        // ── FINALIZAR ──────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log(
            "[MontarSalaHospitalPack] ✓ EntornoSalaUrgencias creado.\n\n" +
            "VERIFICACIONES MANUALES REQUERIDAS:\n" +
            "① Tiles de suelo (floor_1): si flotan o están semienterrados, verificar\n" +
            "   el pivot FBX. Ajustar Y de los GameObjects en Hierarchy si es necesario.\n" +
            "② Paredes (wall_1): confirmar que la cara texturizada mira al interior.\n" +
            "   Si están invertidas, rotar 180° en Y la fila problemática.\n" +
            "③ Plataforma_Entrenamiento: top debe quedar en Y=0.300.\n" +
            "   Entrar en Play y verificar que CalibradorPosicion sitúa el maniquí encima.\n" +
            "④ overhead_light EXCLUIDO (Y=2.338 m > 2 m de sala). Para incluirlo:\n" +
            "   Arrastrarlo desde Assets/Enviroment/HospitalAssets/... y ajustar Y manualmente.\n" +
            "⑤ Material compartido: todos los objetos del pack deben mostrar\n" +
            "   'hospital_gradient_uv' en el Inspector > Materials.\n" +
            "⑥ Bake: Window > Rendering > Lighting > Generate Lighting.\n" +
            "⑦ Build: File > Build Settings → asegurar escena incluida."
        );

        EditorUtility.DisplayDialog("Sala Urgencias armada ✓",
            $"'{ROOT_NAME}' creado con éxito.\n\n" +
            "Revisá la Console para las 7 verificaciones manuales pendientes.", "OK — Ver Console");
    }

    // ─── HELPERS ────────────────────────────────────────────────────────────────

    static GameObject Hijo(GameObject parent, string nombre)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, nombre);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <param name="scale">
    /// Escala uniforme del modelo. Usar ENV_SCALE para sala/mobiliario.
    /// La Plataforma_Entrenamiento siempre pasa 1f explícitamente para no heredar ENV_SCALE.
    /// </param>
    static GameObject InstanciarPack(string modelName, GameObject parent,
        Vector3 pos, Quaternion rot, float scale = 1f)
    {
        string path   = $"{PACK_FOLDER}/{modelName}.fbx";
        var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[Sala] FBX no encontrado: {path}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        Undo.RegisterCreatedObjectUndo(go, go.name);
        go.transform.position   = pos;
        go.transform.rotation   = rot;
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    /// <summary>
    /// Crea un material URP Lit de color sólido sin textura.
    /// Usado para primitivas (Techo Quad) que NO comparten UVs con el atlas del pack.
    /// </summary>
    static Material CrearMatSolido(string nombre, Color color)
    {
        string matPath = $"Assets/Enviroment/HospitalAssets/Materials/{nombre}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;

        var mat = new Material(shader) { name = nombre };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic",   0f);
        mat.SetFloat("_Smoothness", 0.05f);

        string dir = Path.GetDirectoryName(matPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    /// <summary>
    /// Busca el material compartido del pack (generado por Unity al importar con
    /// materialName=0 / By Base Texture Name). Si no existe, lo crea como URP Lit.
    /// </summary>
    static Material ObtenerMaterialPack()
    {
        // 1. Ruta canónica que Unity genera al importar con "By Base Texture Name"
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (mat != null) return mat;

        // 2. Buscar por nombre en todo el proyecto (puede estar en la subcarpeta Materials/ del pack)
        string[] guids = AssetDatabase.FindAssets("hospital_gradient_uv t:Material");
        if (guids.Length > 0)
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (mat != null)
            {
                Debug.Log($"[Sala] Material encontrado en: {AssetDatabase.GUIDToAssetPath(guids[0])}");
                return mat;
            }
        }

        // 3. Crear material URP Lit mínimo con la textura del pack
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURA_PATH);
        if (tex == null)
        {
            Debug.LogWarning($"[Sala] Textura no encontrada en '{TEXTURA_PATH}'. " +
                             "Los objetos quedarán con material magenta hasta asignarlo manualmente.");
            return null;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[Sala] Shader 'Universal Render Pipeline/Lit' no encontrado. " +
                             "Verificar que URP está instalado y configurado.");
            return null;
        }

        mat      = new Material(shader) { name = "hospital_gradient_uv" };
        mat.SetTexture("_BaseMap",   tex);
        mat.SetFloat("_Metallic",    0f);
        mat.SetFloat("_Smoothness",  0.15f);

        string dir = Path.GetDirectoryName(MATERIAL_PATH);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        AssetDatabase.CreateAsset(mat, MATERIAL_PATH);
        AssetDatabase.Refresh();
        Debug.Log($"[Sala] Material creado: {MATERIAL_PATH}");
        return mat;
    }
}
#endif
