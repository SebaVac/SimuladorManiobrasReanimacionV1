#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Capa de ambientación de urgencias para EntornoBoxConsulta (SceneDiagnostico) y EntornoSalaRCP (SceneSimulador).
/// Todo lo que CREA cuelga del root <c>AmbientacionUrgencias</c> (se borra y se recrea en cada ejecución):
///   - <c>Provisional</c>: SOLO primitivas de Unity (cubos y cilindros) fusionadas en pocas mallas (una por elemento:
///     cortina con riel, poste de suero, panel de cabecera con tomas y brazo de monitor, equipo de la puerta con
///     dispensador de alcohol gel y contenedor de cortopunzantes) más las luminarias de techo emisivas. Sin colliders
///     ni scripts, Static. Se borra de un clic con "Quitar ambientación provisional".
///   - <c>Letreros</c> (TextMeshPro, ASCII): "URGENCIAS" sobre la puerta y "BOX 3" en la pared de cabecera.
///   - <c>Sonido</c>: AudioSource propio con el zumbido de ventilación procedural (<see cref="AmbienteSonoroUrgencias"/>).
/// Lo no provisional (letreros, sonido, color y luz) sobrevive a "Quitar ambientación provisional".
///
/// POR QUÉ MALLAS FUSIONADAS: con el SRP Batcher cada renderer cuesta ~2 draw calls aunque esté en un static batch
/// (medido: 23 renderers sueltos = +60 draw calls). Fusionar cada elemento en una malla (1 renderer) y colorear sus
/// partes con casillas planas del atlas del propio pack (UV de todos los vértices al centro de una casilla del
/// degradado) deja la capa en ~5 renderers y un solo material del pack, sin material nuevo por color.
///
/// También MODIFICA, sin crear ni borrar objetos del entorno, (a) el material de las piezas del pack de suelo, techo y
/// paredes (3 copias del material del pack con otro color base, en Assets/Materiales/Ambientacion), (b) los valores
/// y el giro de la luz principal de <see cref="LucesBoxConsulta"/>. Los originales se guardan en
/// <see cref="RegistroAmbientacion"/> y se restauran con "Quitar toda la ambientación".
///
/// No usa audio, texturas ni modelos externos. No toca LogicaRCP, SesionRCP, CalibradorPosicion, el HUD, el
/// OVRCameraRig, PosePaciente, SuperficiePaciente ni el alineador de inicio. Aborta si la escena activa no es
/// SceneDiagnostico ni SceneSimulador.
///
/// Menú: Tools/Ambientación/...
/// </summary>
public static class ConstruirAmbientacionUrgencias
{
    public const string RAIZ = "AmbientacionUrgencias";
    public const string PROVISIONAL = "Provisional";
    const string CARPETA_MAT = "Assets/Materiales/Ambientacion";
    const string CARPETA_PACK = "Assets/Enviroment/HospitalAssets/Models/fbx (Unity Rotated)";

    // Luz y color. 4500-5000 K "blanco neutro" se traduce a un RGB casi blanco con un toque frío: la temperatura de
    // color de Unity (Light.colorTemperature) a 4500-5000 K daría un tinte ANARANJADO, no un blanco frío.
    static readonly Color COLOR_LUZ = new Color(0.95f, 0.97f, 1.00f);
    static readonly Color COLOR_AMBIENTE = new Color(0.60f, 0.62f, 0.66f);
    static readonly Color TINTE_PARED = new Color(0.80f, 0.86f, 0.90f);
    static readonly Color TINTE_SUELO = new Color(0.42f, 0.47f, 0.50f);
    // Techo: con 0,93 el 15-21 % de sus píxeles saturaba (> 0,95) por los puntos de luz bajo el techo y los contornos de
    // las luminarias se perdían. 0,36 de la reflectancia anterior deja p95 en 0,48 (RCP) y 0,65 (Diagnóstico).
    static readonly Color TINTE_TECHO = new Color(0.342f, 0.349f, 0.357f);
    // Píxel blanco uniforme (bloque 32 x 32) del atlas del pack: da un color plano con un material copia.
    static readonly Vector2 UV_BLANCO = new Vector2(0.016f, 0.016f);

    // Casillas del atlas hospital_gradient_uv (8 columnas x 4 filas de 128 x 256 px, filas contadas desde arriba).
    // t = 0 borde superior de la casilla (claro), 1 borde inferior (oscuro).
    static Vector2 Casilla(int col, int fila, float t) => new Vector2((col + 0.5f) / 8f, 1f - (fila + t) / 4f);
    static readonly Vector2 C_METAL = Casilla(4, 2, 0.5f);       // gris
    static readonly Vector2 C_BLANCO = Casilla(3, 2, 0.10f);     // blanco
    static readonly Vector2 C_BOLSA = Casilla(5, 0, 0.20f);      // blanco azulado
    static readonly Vector2 C_CORTINA = Casilla(7, 0, 0.50f);    // verde azulado
    static readonly Vector2 C_VERDE = Casilla(2, 1, 0.80f);      // verde
    static readonly Vector2 C_AMARILLO = Casilla(2, 0, 0.15f);   // amarillo
    static readonly Vector2 C_ROJO = Casilla(4, 1, 0.50f);       // rojo
    static readonly Vector2 C_OSCURO = Casilla(0, 0, 0.95f);     // negro

    static readonly StaticEditorFlags FLAGS_ESTATICOS =
        StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

    [MenuItem("Tools/Ambientación/Construir ambientación de urgencias (escena actual)")]
    public static void Construir()
    {
        string raizEntorno = RaizEntorno();
        if (raizEntorno == null) return;
        bool diagnostico = raizEntorno == "EntornoBoxConsulta";
        var escena = EditorSceneManager.GetActiveScene();
        var entorno = GameObject.Find(raizEntorno);
        if (entorno == null) { Debug.LogError("[Ambientación] No existe " + raizEntorno + " en la escena."); return; }

        // 0. Idempotencia: restaura lo modificado por una ejecución anterior y borra el root
        var previo = GameObject.Find(RAIZ);
        if (previo != null) { Restaurar(previo); Object.DestroyImmediate(previo); }

        var luces = entorno.GetComponentInChildren<LucesBoxConsulta>(true);
        if (luces == null) { Debug.LogError("[Ambientación] Falta LucesBoxConsulta en " + raizEntorno + "."); return; }

        // 1. Root y registro de originales
        var root = new GameObject(RAIZ);
        var registro = root.AddComponent<RegistroAmbientacion>();
        var rends = new List<Renderer>(); var orig = new List<Material>();

        // 2. Materiales: 3 copias del material del pack (pared, suelo, techo) + 1 plano emisivo para las luminarias
        var matPaleta = entorno.transform.Find("Paredes").GetComponentInChildren<MeshRenderer>(true).sharedMaterial;   // material ORIGINAL del pack (wall_1)
        var matPared = CopiaPack("Mat_Amb_Pared", matPaleta, TINTE_PARED, false);
        var matSuelo = CopiaPack("Mat_Amb_Suelo", matPaleta, TINTE_SUELO, true);
        var matTecho = CopiaPack("Mat_Amb_Techo", matPaleta, TINTE_TECHO, true);
        var matLuminaria = Luminaria();
        LimpiarMaterialesViejos();
        Asignar(entorno, "Paredes", matPared, rends, orig);
        Asignar(entorno, "Suelo", matSuelo, rends, orig);
        Asignar(entorno, "Techo", matTecho, rends, orig);
        registro.renderers = rends.ToArray(); registro.materialesOriginales = orig.ToArray();

        // 3. Luz y ambiente (valores serializados de LucesBoxConsulta)
        var soL = new SerializedObject(luces);
        registro.luces = luces;
        registro.colorLuz = soL.FindProperty("colorLuz").colorValue;
        registro.intensidadPrincipal = soL.FindProperty("intensidadPrincipal").floatValue;
        registro.intensidadPuntual = soL.FindProperty("intensidadPuntual").floatValue;
        registro.colorAmbiente = soL.FindProperty("colorAmbiente").colorValue;
        var luzP = soL.FindProperty("luzPrincipal").objectReferenceValue as Light;
        if (luzP != null)
        {
            // La direccional apunta hacia -Z: la pared frontal (detrás del HUD) no recibe sol y deja de ser la superficie más clara
            registro.luzPrincipal = luzP.transform; registro.rotacionLuzPrincipal = luzP.transform.rotation;
            luzP.transform.rotation = Quaternion.Euler(62f, 205f, 0f);
        }
        soL.FindProperty("colorLuz").colorValue = COLOR_LUZ;
        soL.FindProperty("colorAmbiente").colorValue = COLOR_AMBIENTE;
        soL.ApplyModifiedPropertiesWithoutUndo();
        luces.Aplicar();

        // 4. Provisional (primitivas fusionadas)
        var prov = new GameObject(PROVISIONAL); prov.transform.SetParent(root.transform, false);
        if (diagnostico) ProvisionalDiagnostico(prov.transform, matPaleta, matLuminaria); else ProvisionalRCP(prov.transform, matPaleta, matLuminaria);

        // 4b. Esquinas (pieza del pack, no provisional): cierran el hueco de cada esquina de la sala
        EsquinasSala(root.transform, matPared, diagnostico);

        // 5. Letreros (no provisional)
        var gLet = new GameObject("Letreros"); gLet.transform.SetParent(root.transform, false);
        if (diagnostico)
        {
            Letrero(gLet.transform, "Letrero_Puerta", "URGENCIAS", new Vector3(1.25f, 2.18f, -1.5f + 0.012f), Vector3.forward, new Vector2(0.9f, 0.2f), new Color(0.70f, 0.08f, 0.08f));
            Letrero(gLet.transform, "Letrero_Cabecera", "BOX 3", new Vector3(-2.5f + 0.012f, 2.05f, 2.45f), Vector3.right, new Vector2(0.5f, 0.16f), new Color(0.10f, 0.25f, 0.35f));
        }
        else
        {
            Letrero(gLet.transform, "Letrero_Puerta", "URGENCIAS", new Vector3(2.5f, 2.18f, -2.5f + 0.012f), Vector3.forward, new Vector2(0.9f, 0.2f), new Color(0.70f, 0.08f, 0.08f));
            Letrero(gLet.transform, "Letrero_Cabecera", "BOX 3", new Vector3(-3.75f + 0.012f, 2.05f, 2.95f), Vector3.right, new Vector2(0.5f, 0.16f), new Color(0.10f, 0.25f, 0.35f));
        }

        // 6. Sonido ambiente (no provisional): AudioSource propio, separado del metrónomo
        var gSon = new GameObject("Sonido"); gSon.transform.SetParent(root.transform, false);
        var gAmb = new GameObject("Ambiente_Ventilacion"); gAmb.transform.SetParent(gSon.transform, false);
        gAmb.transform.position = new Vector3(0f, 2.3f, diagnostico ? 1.0f : 1.2f);
        gAmb.AddComponent<AudioSource>();
        gAmb.AddComponent<AmbienteSonoroUrgencias>();

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(registro);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[Ambientación] " + escena.name + ": " + CuentaProvisional(prov.transform) + " objetos y " + TrisProvisional(prov.transform) + " triángulos provisionales; 3 materiales copia del pack + 1 emisivo; letreros y sonido creados.");
    }

    [MenuItem("Tools/Ambientación/Quitar ambientación provisional")]
    public static void QuitarProvisional()
    {
        if (RaizEntorno() == null) return;
        var root = GameObject.Find(RAIZ);
        var prov = root != null ? root.transform.Find(PROVISIONAL) : null;
        if (prov == null) { Debug.Log("[Ambientación] No hay ambientación provisional que quitar."); return; }
        Object.DestroyImmediate(prov.gameObject);
        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        Debug.Log("[Ambientación] Provisional quitada. Siguen: luz, color, sonido y letreros.");
    }

    [MenuItem("Tools/Ambientación/Quitar toda la ambientación (restaurar escena)")]
    public static void QuitarTodo()
    {
        if (RaizEntorno() == null) return;
        var root = GameObject.Find(RAIZ);
        if (root == null) { Debug.Log("[Ambientación] No hay ambientación que quitar."); return; }
        Restaurar(root);
        Object.DestroyImmediate(root);
        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena);
        Debug.Log("[Ambientación] Ambientación quitada y escena restaurada (materiales, luces y giro de la luz principal originales).");
    }

    // ── Utilidades de escena ────────────────────────────────────────────────────────────────────────────────────
    static string RaizEntorno()
    {
        var n = EditorSceneManager.GetActiveScene().name;
        if (n == "SceneDiagnostico") return "EntornoBoxConsulta";
        if (n == "SceneSimulador") return "EntornoSalaRCP";
        EditorUtility.DisplayDialog("Escena incorrecta", "Abre SceneDiagnostico o SceneSimulador.\nActual: " + n, "OK");
        return null;
    }

    static void Restaurar(GameObject root)
    {
        var reg = root.GetComponent<RegistroAmbientacion>();
        if (reg == null) return;
        if (reg.renderers != null)
            for (int i = 0; i < reg.renderers.Length; i++)
                if (reg.renderers[i] != null && reg.materialesOriginales[i] != null) reg.renderers[i].sharedMaterial = reg.materialesOriginales[i];
        if (reg.luzPrincipal != null) reg.luzPrincipal.rotation = reg.rotacionLuzPrincipal;
        var luces = reg.luces as LucesBoxConsulta;
        if (luces != null)
        {
            var so = new SerializedObject(luces);
            so.FindProperty("colorLuz").colorValue = reg.colorLuz;
            so.FindProperty("intensidadPrincipal").floatValue = reg.intensidadPrincipal;
            so.FindProperty("intensidadPuntual").floatValue = reg.intensidadPuntual;
            so.FindProperty("colorAmbiente").colorValue = reg.colorAmbiente;
            so.ApplyModifiedPropertiesWithoutUndo();
            luces.Aplicar();
        }
    }

    static void Asignar(GameObject entorno, string grupo, Material m, List<Renderer> rends, List<Material> orig)
    {
        var g = entorno.transform.Find(grupo); if (g == null) return;
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
        {
            rends.Add(r); orig.Add(r.sharedMaterial);
            r.sharedMaterial = m;
        }
    }

    static int CuentaProvisional(Transform t) => t.GetComponentsInChildren<Transform>(true).Length - 1;
    static int TrisProvisional(Transform t)
    {
        int n = 0;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true)) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) n += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
        return n;
    }

    // ── Materiales ──────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>Copia del material del pack con otro color base. 'plano' = todos los UV apuntan a un píxel blanco del
    /// atlas (color uniforme, sin el damero del pack).</summary>
    static Material CopiaPack(string nombre, Material fuente, Color tinte, bool plano)
    {
        AsegurarCarpeta();
        string ruta = CARPETA_MAT + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(fuente); AssetDatabase.CreateAsset(m, ruta); }
        else { m.shader = fuente.shader; m.CopyPropertiesFromMaterial(fuente); }
        m.name = nombre;
        m.SetColor("_BaseColor", tinte);
        m.SetFloat("_Smoothness", 0.1f);
        m.enableInstancing = true;
        if (plano) { m.SetTextureScale("_BaseMap", new Vector2(0.0001f, 0.0001f)); m.SetTextureOffset("_BaseMap", UV_BLANCO); }
        else { m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero); }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Material plano sin luz (Unlit blanco): las luminarias de techo se ven emisivas sin añadir luces.</summary>
    static Material Luminaria()
    {
        AsegurarCarpeta();
        string ruta = CARPETA_MAT + "/Mat_Amb_Luminaria.mat";
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, ruta); }
        m.shader = shader; m.name = "Mat_Amb_Luminaria";
        m.SetColor("_BaseColor", Color.white);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Materiales planos de una versión anterior (un material por color): ya no se usan
    static void LimpiarMaterialesViejos()
    {
        foreach (var n in new[] { "Cortina", "Metal", "Verde", "Amarillo", "Blanco", "Rojo", "PantallaOscura", "Bolsa" })
        {
            string ruta = CARPETA_MAT + "/Mat_Amb_" + n + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
        }
    }

    static void AsegurarCarpeta()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(CARPETA_MAT)) AssetDatabase.CreateFolder("Assets/Materiales", "Ambientacion");
    }

    // ── Primitivas fusionadas ───────────────────────────────────────────────────────────────────────────────────
    static Mesh _cubo, _cilindro;

    static void Parte(List<CombineInstance> lista, bool cilindro, Vector3 centro, Vector3 tam, Quaternion rot, Vector2 uv)
    {
        if (_cilindro == null)
        {
            // La malla de la primitiva mide 1 x 2 x 1 (diámetro 1, alto 2); "Cylinder.fbx" de los recursos integrados mide 2 x 2 x 2
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _cilindro = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
        }
        if (_cubo == null) _cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var copia = Object.Instantiate(cilindro ? _cilindro : _cubo);
        var uvs = new Vector2[copia.vertexCount]; for (int i = 0; i < uvs.Length; i++) uvs[i] = uv;
        copia.uv = uvs;
        lista.Add(new CombineInstance { mesh = copia, transform = Matrix4x4.TRS(centro, rot, tam) });
    }

    static void Cubo(List<CombineInstance> l, Vector3 c, Vector3 tam, Vector2 uv, float rotY = 0f)
        => Parte(l, false, c, tam, Quaternion.Euler(0f, rotY, 0f), uv);

    /// <summary>Cilindro de diámetro 'd' y alto 'h' con el eje en la dirección 'eje' (por defecto vertical).</summary>
    static void Cilindro(List<CombineInstance> l, Vector3 c, float d, float h, Vector2 uv, Vector3? eje = null)
        => Parte(l, true, c, new Vector3(d, h / 2f, d), Quaternion.FromToRotation(Vector3.up, eje ?? Vector3.up), uv);

    /// <summary>Crea un GameObject con una malla fusionada (1 renderer) a partir de las partes, con el material dado.</summary>
    static GameObject Fusionar(Transform padre, string nombre, List<CombineInstance> partes, Material m, Vector3 pos, float rotY)
    {
        var malla = new Mesh { name = "Malla_" + nombre };
        malla.CombineMeshes(partes.ToArray(), true, true);
        malla.RecalculateBounds();
        foreach (var p in partes) Object.DestroyImmediate(p.mesh);
        var go = new GameObject(nombre); go.transform.SetParent(padre, false);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(go, FLAGS_ESTATICOS);
        return go;
    }

    // ── Elementos (coordenadas locales del soporte: +Z hacia el interior de la sala, +X a lo largo de la pared) ────
    static void Luminarias(Transform p, Material mLum, params Vector2[] xz)
    {
        var l = new List<CombineInstance>();
        foreach (var v in xz) Cubo(l, new Vector3(v.x, 2.49f, v.y), new Vector3(0.6f, 0.02f, 1.2f), C_BLANCO);
        Fusionar(p, "Luminarias", l, mLum, Vector3.zero, 0f);
    }

    static void Cortina(Transform p, Material m, Vector3 ini, Vector3 fin, int n)
    {
        var l = new List<CombineInstance>();
        Vector3 d = fin - ini; d.y = 0f; float L = d.magnitude; d /= L;
        float rotY = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
        Vector3 mid = (ini + fin) / 2f;
        Cubo(l, new Vector3(mid.x, 2.43f, mid.z), new Vector3(L, 0.03f, 0.03f), C_METAL, rotY);            // riel de techo
        float paso = L / n; float ancho = paso * 0.97f; Vector3 normal = new Vector3(-d.z, 0f, d.x);
        for (int i = 0; i < n; i++)
        {
            Vector3 c = ini + d * (paso * (i + 0.5f)) + normal * ((i % 2 == 0 ? 1f : -1f) * 0.025f);       // pliegues alternados
            Cubo(l, new Vector3(c.x, 1.37f, c.z), new Vector3(ancho, 2.1f, 0.022f), C_CORTINA, rotY);
        }
        Fusionar(p, "Cortina_Derecha", l, m, Vector3.zero, 0f);
    }

    static void PosteSuero(Transform p, Material m, float x, float z)
    {
        var l = new List<CombineInstance>();
        Cilindro(l, new Vector3(0f, 0.015f, 0f), 0.45f, 0.03f, C_METAL);                  // base
        Cilindro(l, new Vector3(0f, 1.03f, 0f), 0.03f, 2.0f, C_METAL);                    // poste
        Cubo(l, new Vector3(0.06f, 2.04f, 0f), new Vector3(0.30f, 0.02f, 0.02f), C_METAL); // barra
        Cubo(l, new Vector3(0.12f, 1.88f, 0f), new Vector3(0.13f, 0.24f, 0.04f), C_BOLSA); // bolsa lisa, sin etiqueta
        Fusionar(p, "PosteSuero", l, m, new Vector3(x, 0f, z), 0f);
    }

    /// <summary>Panel de cabecera con tomas (oxígeno verde, aspiración amarillo, aire blanco) y brazo de monitor con
    /// pantalla apagada. 'pos' = punto de la pared; el +Z local apunta al interior; +X local corre a lo largo de la pared.</summary>
    static void PanelCabecera(Transform p, Material m, Vector3 pos, float rotY)
    {
        var l = new List<CombineInstance>();
        Cubo(l, new Vector3(0f, 1.5f, 0.035f), new Vector3(0.80f, 0.22f, 0.07f), C_METAL);
        Vector3 eje = Vector3.forward;
        Cilindro(l, new Vector3(-0.22f, 1.5f, 0.085f), 0.07f, 0.03f, C_VERDE, eje);
        Cilindro(l, new Vector3(0f, 1.5f, 0.085f), 0.07f, 0.03f, C_AMARILLO, eje);
        Cilindro(l, new Vector3(0.22f, 1.5f, 0.085f), 0.07f, 0.03f, C_BLANCO, eje);
        Cubo(l, new Vector3(0.55f, 1.72f, 0.06f), new Vector3(0.30f, 0.035f, 0.035f), C_METAL);
        Cubo(l, new Vector3(0.68f, 1.72f, 0.21f), new Vector3(0.035f, 0.035f, 0.30f), C_METAL);
        Cubo(l, new Vector3(0.68f, 1.60f, 0.37f), new Vector3(0.30f, 0.20f, 0.03f), C_OSCURO);          // pantalla apagada, tono uniforme
        Fusionar(p, "PanelCabecera", l, m, pos, rotY);
    }

    /// <summary>Equipo de la puerta: dispensador de alcohol gel (blanco) y contenedor de cortopunzantes (rojo con tapa amarilla).</summary>
    static void EquipoPuerta(Transform p, Material m, float xGel, float xCorto, float zPared)
    {
        var l = new List<CombineInstance>();
        Cubo(l, new Vector3(xGel, 1.25f, zPared + 0.05f), new Vector3(0.11f, 0.26f, 0.10f), C_BLANCO);
        Cubo(l, new Vector3(xGel, 1.15f, zPared + 0.12f), new Vector3(0.04f, 0.03f, 0.07f), C_METAL);
        Cubo(l, new Vector3(xCorto, 1.05f, zPared + 0.075f), new Vector3(0.26f, 0.32f, 0.15f), C_ROJO);
        Cubo(l, new Vector3(xCorto, 1.23f, zPared + 0.08f), new Vector3(0.27f, 0.04f, 0.16f), C_AMARILLO);
        Fusionar(p, "EquipoPuerta_GelYCortopunzantes", l, m, Vector3.zero, 0f);
    }

    // ── Disposición por escena ──────────────────────────────────────────────────────────────────────────────────
    static void ProvisionalDiagnostico(Transform p, Material m, Material mLum)
    {
        Luminarias(p, mLum, new Vector2(-1.0f, 0.6f), new Vector2(1.0f, 0.6f), new Vector2(0.0f, 2.4f));
        Cortina(p, m, new Vector3(1.95f, 0f, 1.45f), new Vector3(1.95f, 0f, 3.05f), 4);       // lado derecho, fuera del metro libre de la camilla
        PosteSuero(p, m, -2.15f, 3.0f);                                                       // junto a la cabecera, rincón del fondo
        PanelCabecera(p, m, new Vector3(-2.5f, 0f, 2.45f), 90f);                              // pared izquierda (+Z local = +X mundo)
        EquipoPuerta(p, m, 0.45f, 2.05f, -1.5f);                                              // pared trasera; la puerta ocupa x 0,75-1,75
    }

    static void ProvisionalRCP(Transform p, Material m, Material mLum)
    {
        Luminarias(p, mLum, new Vector2(-1.2f, -0.2f), new Vector2(1.2f, 1.2f), new Vector2(0.0f, 3.6f));
        Cortina(p, m, new Vector3(3.2f, 0f, 3.0f), new Vector3(3.2f, 0f, 4.4f), 4);
        PosteSuero(p, m, -3.2f, 4.1f);                                                       // rincón delantero izquierdo, junto a los gabinetes
        PanelCabecera(p, m, new Vector3(-3.75f, 0f, 2.95f), 90f);
        EquipoPuerta(p, m, 1.55f, 3.45f, -2.5f);                                              // la puerta ocupa x 2,0-3,0
    }

    // ── Esquinas ────────────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>La cara interior visible de wall_1 está 9,5 cm detrás de la línea nominal de la pared; en cada esquina de la
    /// sala las dos paredes dejan por eso un hueco cuadrado de ~9,5 cm por el que se ve el fondo de la cámara (medido con
    /// rayos: sin impactos). El pack trae para eso la pieza wall_column (0,38 m a escala 1; 0,47 m a 1,25). Se funden las 4
    /// columnas en UNA malla (1 renderer) con el material de la pared.</summary>
    static void EsquinasSala(Transform root, Material matPared, bool diagnostico)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CARPETA_PACK + "/wall_column.fbx");
        if (prefab == null) { Debug.LogError("[Ambientación] FBX no encontrado: wall_column"); return; }
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        Matrix4x4 hijoEnRaiz = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        float xMin = diagnostico ? -2.5f : -3.75f, xMax = -xMin, zMin = diagnostico ? -1.5f : -2.5f, zMax = diagnostico ? 3.5f : 5.0f;
        // La columna ocupa x[0;0,378] z[-0,378;0] y mira hacia (-x,+z): rotY 0 en (xMax,zMin), 90 en (xMin,zMin), 180 en (xMin,zMax), 270 en (xMax,zMax)
        var esquinas = new[] { new Vector3(xMax, 0f, zMin), new Vector3(xMin, 0f, zMin), new Vector3(xMin, 0f, zMax), new Vector3(xMax, 0f, zMax) };
        var rot = new[] { 0f, 90f, 180f, 270f };
        var partes = new List<CombineInstance>();
        for (int i = 0; i < 4; i++)
            partes.Add(new CombineInstance { mesh = mf.sharedMesh, transform = Matrix4x4.TRS(esquinas[i], Quaternion.Euler(0f, rot[i], 0f), Vector3.one * 1.25f) * hijoEnRaiz });
        var malla = new Mesh { name = "Malla_Esquinas" };
        malla.CombineMeshes(partes.ToArray(), true, true);
        malla.RecalculateBounds();
        var g = new GameObject("Esquinas"); g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = matPared;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(g, FLAGS_ESTATICOS);
    }

    // ── Letreros (TextMeshPro, LiberationSans, ASCII) ──────────────────────────────────────────────────────────
    static void Letrero(Transform padre, string nombre, string texto, Vector3 pos, Vector3 normalInterior, Vector2 tam, Color color)
    {
        var go = new GameObject(nombre); go.transform.SetParent(padre, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(-normalInterior, Vector3.up);       // se lee desde dentro de la sala
        var t = go.AddComponent<TextMeshPro>();
        t.font = TMP_Settings.defaultFontAsset;                                              // LiberationSans SDF
        t.text = texto;
        t.rectTransform.sizeDelta = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.color = color.linear;      // el shader de TMP no convierte el color del vértice a lineal: sin esto el texto se ve más claro
        t.enableWordWrapping = true;
        t.enableAutoSizing = true; t.fontSizeMin = 0.2f; t.fontSizeMax = 3f;
        t.overflowMode = TextOverflowModes.Truncate;
        t.raycastTarget = false;
        var mr = go.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }
}
#endif
