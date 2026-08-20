#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

/// <summary>
/// Menú: Tools > Crear Entorno Sala Urgencias
/// Crea la geometría de sala de urgencias para el simulador RCP.
/// Ejecutar una única vez desde el Editor con la escena SampleScene abierta.
/// </summary>
public static class CrearEntornoSalaUrgencias
{
    const string MAT_FOLDER = "Assets/Materiales";

    [MenuItem("Tools/Crear Entorno Sala Urgencias")]
    static void Crear()
    {
        if (GameObject.Find("EntornoSalaUrgencias") != null)
        {
            EditorUtility.DisplayDialog("Entorno Urgencias",
                "EntornoSalaUrgencias ya existe en la escena.\n" +
                "Eliminalo del Hierarchy antes de recrearlo.", "OK");
            return;
        }

        // ── MATERIALES ────────────────────────────────────────────────────
        var matParedes = MatLit("Mat_Sala_Paredes",   new Color(0.82f, 0.80f, 0.76f));
        var matSuelo   = MatLit("Mat_Sala_Suelo",     new Color(0.50f, 0.56f, 0.50f));
        var matTecho   = MatLit("Mat_Sala_Techo",     new Color(0.91f, 0.91f, 0.89f));
        var matCamilla = MatLit("Mat_Camilla",        new Color(0.88f, 0.88f, 0.88f), 0.15f, 0.4f);
        var matMetal   = MatLit("Mat_Monitor_Cuerpo", new Color(0.18f, 0.18f, 0.20f), 0.50f, 0.6f);

        // Reutilizar material holográfico existente para pantalla del monitor
        var matHolo   = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_FOLDER}/Mat_Holograma.mat");
        var matScreen = matHolo != null ? matHolo : matMetal;

        AssetDatabase.SaveAssets();

        // ── RAÍZ ──────────────────────────────────────────────────────────
        var root = new GameObject("EntornoSalaUrgencias");
        Undo.RegisterCreatedObjectUndo(root, "Crear EntornoSalaUrgencias");

        // ── GEOMETRÍA DE SALA ─────────────────────────────────────────────
        // Dimensiones: 5 m ancho (X: −2.5 … +2.5) × 6 m largo (Z: −2 … +4) × 3 m alto
        // Origen del jugador en (0,0,0); Paciente_Completo en (0,0,1.025)
        var geo = Hijo(root, "Geometria_Sala");

        Quad(geo, "Suelo",           pos(0,      0,    1),    rot(-90,   0, 0), escala(5, 6, 1), matSuelo,   ShadowCastingMode.Off);
        Quad(geo, "Techo",           pos(0,      3,    1),    rot( 90,   0, 0), escala(5, 6, 1), matTecho,   ShadowCastingMode.Off);
        Quad(geo, "Pared_Posterior", pos(0,      1.5f, 4),    rot(  0, 180, 0), escala(5, 3, 1), matParedes);
        Quad(geo, "Pared_Frontal",   pos(0,      1.5f,-2),    rot(  0,   0, 0), escala(5, 3, 1), matParedes);
        Quad(geo, "Pared_Derecha",   pos( 2.5f,  1.5f, 1),   rot(  0, -90, 0), escala(6, 3, 1), matParedes);
        Quad(geo, "Pared_Izquierda", pos(-2.5f,  1.5f, 1),   rot(  0,  90, 0), escala(6, 3, 1), matParedes);

        // ── CAMILLA ───────────────────────────────────────────────────────
        // Superficie superior en Y=0.3 (= CalibradorPosicion.alturaSuelo)
        // Paciente_Completo se mueve a Y=0.3 en runtime por CalibradorPosicion.Start()
        // Centro horizontal alineado con Paciente_Completo: Z=1.025
        var camilla = Hijo(root, "Camilla");

        //  Superficie: center Y=0.275, mitad=0.025 → top en Y=0.300
        Cubo(camilla, "Superficie",            pos(  0,    0.275f, 1.025f), escala(0.65f, 0.05f, 2.0f),  matCamilla);

        // Patas: center Y=0.125, mitad=0.125 → van de Y=0 a Y=0.25 (= base de superficie)
        Cubo(camilla, "Pata_FrontalDerecha",   pos( 0.28f, 0.125f, 1.925f), escala(0.05f, 0.25f, 0.05f), matCamilla);
        Cubo(camilla, "Pata_FrontalIzquierda", pos(-0.28f, 0.125f, 1.925f), escala(0.05f, 0.25f, 0.05f), matCamilla);
        Cubo(camilla, "Pata_TraseraDerecha",   pos( 0.28f, 0.125f, 0.125f), escala(0.05f, 0.25f, 0.05f), matCamilla);
        Cubo(camilla, "Pata_TraseraIzquierda", pos(-0.28f, 0.125f, 0.125f), escala(0.05f, 0.25f, 0.05f), matCamilla);

        // ── MONITOR MÉDICO ────────────────────────────────────────────────
        // A la izquierda del paciente (X=−1.4), pantalla mirando +X (rot Y=+90)
        // Cuerpo del monitor: delgado en X (0.06 m), ancho en Z (0.44 m)
        // Pantalla Quad con normal apuntando +X → rot(0,90,0)
        var monitor = Hijo(root, "Monitor_Medico");

        Cubo(monitor, "Soporte",        pos(-1.4f,  0.65f, 1.0f), escala(0.04f,  1.30f, 0.04f), matMetal);
        Cubo(monitor, "Cuerpo_Monitor", pos(-1.4f,  1.4f,  1.0f), escala(0.06f,  0.34f, 0.44f), matMetal);
        Quad(monitor, "Pantalla",       pos(-1.37f, 1.4f,  1.0f), rot(0, 90, 0), escala(0.38f, 0.28f, 1), matScreen, ShadowCastingMode.Off);

        // ── DESHABILITAR OVRPassthroughLayer ─────────────────────────────
        // Cambio de MR a VR puro: el passthrough ya no se usará
#pragma warning disable CS0618
        var passthrough = Object.FindObjectOfType<OVRPassthroughLayer>();
#pragma warning restore CS0618
        if (passthrough != null)
        {
            Undo.RecordObject(passthrough, "Disable OVRPassthroughLayer");
            passthrough.enabled = false;
            Debug.Log("[EntornoSalaUrgencias] OVRPassthroughLayer deshabilitado.");
        }
        else
        {
            Debug.LogWarning("[EntornoSalaUrgencias] OVRPassthroughLayer no encontrado — deshabilitalo manualmente en el Inspector.");
        }

        // ── DIRECTIONAL LIGHT → BAKED ─────────────────────────────────────
#pragma warning disable CS0618
        foreach (var l in Object.FindObjectsOfType<Light>())
#pragma warning restore CS0618
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "Set Directional Light Baked");
            l.lightmapBakeType = LightmapBakeType.Baked;
            Debug.Log("[EntornoSalaUrgencias] Directional Light cambiado a Baked.");
            break;
        }

        // ── CÁMARA: FONDO NEGRO SÓLIDO ────────────────────────────────────
        // En VR puro el fondo debe ser negro opaco, no transparente (passthrough)
#pragma warning disable CS0618
        foreach (var cam in Object.FindObjectsOfType<Camera>())
#pragma warning restore CS0618
        {
            Undo.RecordObject(cam, "Camera Background Black");
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        // ── GUARDAR ───────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[EntornoSalaUrgencias] ¡Entorno creado! Pasos siguientes:\n" +
                  "  1. En Hierarchy, seleccioná todos los hijos de Geometria_Sala\n" +
                  "     → Inspector → tildá 'Static' (para lightmapping)\n" +
                  "  2. Window > Rendering > Lighting > Generate Lighting\n" +
                  "  3. Guardá la escena: Ctrl+S");

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    // ── HELPERS ───────────────────────────────────────────────────────────

    /// Crea un GameObject vacío como hijo del padre dado.
    static GameObject Hijo(GameObject padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre.transform, worldPositionStays: false);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        return go;
    }

    /// Crea un Quad primitivo sin Collider, con posición/rotación/escala en world space.
    static void Quad(GameObject padre, string nombre,
                     Vector3 wPos, Vector3 wRot, Vector3 wScale,
                     Material mat,
                     ShadowCastingMode shadows = ShadowCastingMode.On)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = nombre;
        go.transform.position    = wPos;
        go.transform.eulerAngles = wRot;
        go.transform.localScale  = wScale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial    = mat;
        r.shadowCastingMode = shadows;
        Object.DestroyImmediate(go.GetComponent<MeshCollider>());
        go.transform.SetParent(padre.transform, worldPositionStays: true);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
    }

    /// Crea un Cubo primitivo sin Collider, con posición/escala en world space.
    static void Cubo(GameObject padre, string nombre,
                     Vector3 wPos, Vector3 wScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.position   = wPos;
        go.transform.localScale = wScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.SetParent(padre.transform, worldPositionStays: true);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
    }

    /// Crea (o reutiliza si ya existe) un material URP Lit en Assets/Materiales/.
    static Material MatLit(string nombre, Color color, float metallic = 0f, float smoothness = 0.3f)
    {
        string path = $"{MAT_FOLDER}/{nombre}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[EntornoSalaUrgencias] Shader 'Universal Render Pipeline/Lit' no encontrado.");
            shader = Shader.Find("Standard");
        }
        var mat = new Material(shader) { name = nombre };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic",   metallic);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Aliases para mejorar la legibilidad del código de creación
    static Vector3 pos(float x, float y, float z)    => new Vector3(x, y, z);
    static Vector3 rot(float x, float y, float z)    => new Vector3(x, y, z);
    static Vector3 escala(float x, float y, float z) => new Vector3(x, y, z);
}
#endif
