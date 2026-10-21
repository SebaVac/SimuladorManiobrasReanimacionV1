#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Construye en SceneSimulador (módulo de RCP) la sala <c>EntornoSalaRCP</c>, coherente con el box de consulta de
/// SceneDiagnostico: SOLO piezas del Hospital Pack que el proyecto ya tiene, a la MISMA escala uniforme (1,25) y
/// alineadas a la rejilla de 2,5 m. No importa ni crea piezas, texturas ni personajes. Idempotente: destruye y
/// recrea <c>EntornoSalaRCP</c>. Desactiva (SetActive false, sin borrar) <c>EntornoSalaUrgencias</c> y
/// <c>REF_Humano_1.60m</c>. Aborta si la escena activa no es SceneSimulador.
///
/// Dimensiones (m), dictadas por la auditoría:
///   - 7,5 x 7,5 x 2,5: X[-3,75; +3,75], Z[-2,5; +5,0]. Punto de inicio del jugador en (0,0), mirando +Z.
///   - Pared trasera a 2,5 m del punto de inicio (mínimo exigido 1,5 m); paredes laterales a 3,75 m; pared frontal
///     a 5,0 m (mínimo 3,01 m = Panel_MetricasRCP en z 2,51 + 0,5 m).
///   - El ancho de 7,5 m deja que el paciente (largo ~1,83 m, tumbado a lo largo de X) se calibre hasta |x| = 1,5 m
///     sin tocar las paredes.
/// El techo se arma con floor_1 boca abajo (el pack no trae pieza de techo), como en el box.
/// El mobiliario va contra las paredes, fuera de la zona de trabajo (paciente + 1,0 m), sin colliders ni scripts.
/// La superficie bajo el paciente es un tablero (wall_base) que coloca <see cref="SuperficiePaciente"/>: el pack no
/// trae una camilla sin pedestal, y una cama con pedestal quedaría flotando cuando el maniquí está sobre una mesa.
///
/// No toca: LogicaRCP, SesionRCP, CalibradorPosicion, BotonMaestro, paneles, OVRCameraRig, SceneDiagnostico,
/// ConstruirBoxConsultaDiagnostico ni MontarSalaHospitalPack (no se ejecutan).
///
/// Menú: Tools/RCP/Construir sala de RCP (SceneSimulador)
/// </summary>
public static class ConstruirSalaRCP
{
    public const string RAIZ = "EntornoSalaRCP";
    const string CARPETA_PACK = "Assets/Enviroment/HospitalAssets/Models/fbx (Unity Rotated)";

    // ── Geometría (m). Todo el pack a la MISMA escala uniforme, igual que el box de consulta ───────────────────────
    // Escala 1,25 => módulo (losa de suelo y paño de pared) de 2,5 m y altura de pared de 2,5 m. Los paneles
    // head-locked llegan a +0,45 m sobre los ojos: con ojos a 1,7 m quedan por debajo del techo.
    public const float ESCALA = 1.25f;
    public const float MODULO = 2f * ESCALA;                // 2,5 m
    public const float X_MIN = -1.5f * MODULO, X_MAX = 1.5f * MODULO;   // 7,5 m de ancho
    public const float Z_MIN = -MODULO, Z_MAX = 2f * MODULO;            // 7,5 m de largo
    public const float ALTO = MODULO;

    // Tablero bajo el paciente (wall_base estirado: es una losa lisa, el estiramiento no se nota)
    const float TABLERO_ANCHO = 0.80f, TABLERO_GRUESO = 0.06f, TABLERO_HOLGURA_LARGO = 0.20f;
    // wall_base en sus unidades locales: 2,0 (x) x 0,18 (y, hacia abajo desde 0) x 0,31 (z, hacia atrás desde 0)
    const float WB_X = 2.0f, WB_Y = 0.18f, WB_Z = 0.31f;

    static readonly StaticEditorFlags FLAGS_ESTATICOS =
        StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

    [MenuItem("Tools/RCP/Construir sala de RCP (SceneSimulador)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneSimulador")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre SceneSimulador antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }
        var pac = GameObject.Find("Paciente_Completo");
        var smr = pac != null && pac.transform.Find("Banana Man/Body") != null ? pac.transform.Find("Banana Man/Body").GetComponent<SkinnedMeshRenderer>() : null;
        Transform hips = null, head = null;
        if (pac != null) foreach (var t in pac.GetComponentsInChildren<Transform>(true)) { if (t.name == "Hips") hips = t; else if (t.name == "Head") head = t; }
        if (pac == null || smr == null || hips == null || head == null)
        {
            Debug.LogError("[SalaRCP] Falta Paciente_Completo, Banana Man/Body, Hips o Head.");
            return;
        }
        if (Object.FindFirstObjectByType<AlineadorInicioJugador>() == null || Object.FindFirstObjectByType<PuntoInicioJugador>() == null)
        {
            Debug.LogError("[SalaRCP] Falta AlineadorInicioJugador o PuntoInicioJugador: ejecuta antes Tools/Inicio del jugador/Configurar escena actual.");
            return;
        }

        // ── 1. Entorno viejo y restos fuera de él (se desactivan, no se borran) ─────────────────────────────────
        var viejo = GameObject.Find("EntornoSalaUrgencias"); if (viejo != null) viejo.SetActive(false);
        var refHumano = GameObject.Find("REF_Humano_1.60m"); if (refHumano != null) refHumano.SetActive(false);

        // ── 2. Entorno nuevo ────────────────────────────────────────────────────────────────────────────────────
        var previo = GameObject.Find(RAIZ); if (previo != null) Object.DestroyImmediate(previo);
        var root = new GameObject(RAIZ);
        var gSuelo = Hijo(root, "Suelo"); var gTecho = Hijo(root, "Techo"); var gParedes = Hijo(root, "Paredes");
        var gMob = Hijo(root, "Mobiliario"); var gLuces = Hijo(root, "Luces");

        float[] cx = { -MODULO, 0f, MODULO };                                        // -2,5 / 0 / +2,5
        float[] cz = { Z_MIN + MODULO / 2f, Z_MIN + 1.5f * MODULO, Z_MIN + 2.5f * MODULO };   // -1,25 / 1,25 / 3,75
        foreach (float x in cx) foreach (float z in cz)
        {
            Pieza("floor_1", gSuelo, new Vector3(x, 0f, z), 0f, ESCALA);
            var techo = Pieza("floor_1", gTecho, new Vector3(x, ALTO, z), 0f, ESCALA);   // losa boca abajo = techo
            if (techo != null) techo.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        }
        // Paredes: la cara interior mira hacia +Z local. Trasera con puerta (detrás del jugador), derecha con ventana.
        Pieza("wall_1",    gParedes, new Vector3(cx[0], 0f, Z_MIN), 0f, ESCALA);
        Pieza("wall_1",    gParedes, new Vector3(cx[1], 0f, Z_MIN), 0f, ESCALA);
        Pieza("wall_door", gParedes, new Vector3(cx[2], 0f, Z_MIN), 0f, ESCALA);
        for (int i = 0; i < 3; i++) Pieza("wall_1", gParedes, new Vector3(cx[i], 0f, Z_MAX), 180f, ESCALA);
        for (int i = 0; i < 3; i++) Pieza("wall_1", gParedes, new Vector3(X_MIN, 0f, cz[i]), 90f, ESCALA);
        Pieza("wall_1",      gParedes, new Vector3(X_MAX, 0f, cz[0]), 270f, ESCALA);
        Pieza("wall_1",      gParedes, new Vector3(X_MAX, 0f, cz[1]), 270f, ESCALA);
        Pieza("wall_window", gParedes, new Vector3(X_MAX, 0f, cz[2]), 270f, ESCALA);

        // Mobiliario SOLO contra las paredes y fuera de la zona de trabajo (paciente + 1,0 m). Sin colliders ni scripts.
        // Pared izquierda: dos gabinetes (bajo + alto) y un carro de equipo.
        foreach (float z in new[] { 0.6f, 1.6f }) Gabinete(gMob, 'I', z);
        ContraPared("machine_2", gMob, 'I', 3.4f, 0f);
        // Pared derecha (con la ventana al fondo): monitor y carro de equipo.
        ContraPared("machine_1", gMob, 'D', 0.3f, 0f);
        ContraPared("machine_2", gMob, 'D', 1.4f, 0f);
        // Pared frontal (detrás del HUD): gabinetes en la esquina izquierda y un monitor en la derecha; el paño
        // que queda detrás de Panel_MetricasRCP y Panel_GuiaManos (x -1,5 ... 1,6) se deja liso.
        foreach (float x in new[] { -2.9f, -1.95f }) Gabinete(gMob, 'F', x);
        ContraPared("machine_1", gMob, 'F', 2.9f, 0f);
        // Pared trasera (a espaldas del jugador): gabinete y carro.
        Gabinete(gMob, 'T', -2.9f);
        ContraPared("machine_2", gMob, 'T', -1.5f, 0f);

        // Luces: 1 direccional + 2 puntuales de techo, tiempo real, sin sombras (componente del box, reutilizado).
        // La direccional apunta hacia -Z: la pared frontal (la que queda detrás del HUD) no recibe sol y es la más
        // oscura de la sala, así los paneles Canvas translúcidos se leen sobre un fondo medio y no sobre el más claro.
        var gl = new GameObject("Luz_Principal"); gl.transform.SetParent(gLuces.transform, false);
        gl.transform.rotation = Quaternion.Euler(62f, 205f, 0f);
        var lp = gl.AddComponent<Light>(); lp.type = LightType.Directional; lp.shadows = LightShadows.None; lp.lightmapBakeType = LightmapBakeType.Realtime;
        var puntuales = new Light[2]; Vector3[] pp2 = { new Vector3(-1.2f, ALTO - 0.55f, -0.4f), new Vector3(1.2f, ALTO - 0.55f, 1.2f) };
        for (int i = 0; i < 2; i++)
        {
            var g = new GameObject("Luz_Techo_" + (i + 1)); g.transform.SetParent(gLuces.transform, false); g.transform.position = pp2[i];
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Realtime; puntuales[i] = l;
        }
        var luces = root.AddComponent<LucesBoxConsulta>();
        var soL = new SerializedObject(luces);
        soL.FindProperty("luzPrincipal").objectReferenceValue = lp;
        var arr = soL.FindProperty("puntualesTecho"); arr.arraySize = 2; for (int i = 0; i < 2; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = puntuales[i];
        soL.ApplyModifiedPropertiesWithoutUndo(); luces.Aplicar();

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) GameObjectUtility.SetStaticEditorFlags(mr.gameObject, FLAGS_ESTATICOS);

        // ── 3. Superficie bajo el paciente ──────────────────────────────────────────────────────────────────────
        // Se mide con el paciente tal como está (escala y giro actuales) y se normaliza a escala 1.
        float escalaPac = pac.transform.lossyScale.y;
        int[] dom; Vector3[] piel = ConstruirBoxConsultaDiagnostico.PielMundo(smr, out dom);
        Vector3 hp = hips.position;
        Vector3 aPies = hp - head.position; aPies.y = 0f; aPies.Normalize();           // eje del cuerpo, de la cabeza a los pies
        float yawEje = Mathf.Atan2(-aPies.z, aPies.x) * Mathf.Rad2Deg;                  // yaw tal que Euler(0,yaw,0)*right = aPies
        float giro = Mathf.DeltaAngle(pac.transform.eulerAngles.y, yawEje);
        float minE = float.MaxValue, maxE = float.MinValue, minYespalda = float.MaxValue;
        for (int i = 0; i < piel.Length; i++)
        {
            float e = Vector3.Dot(piel[i] - hp, aPies);
            if (e < minE) minE = e; if (e > maxE) maxE = e;
            string hueso = smr.bones[dom[i]].name;                         // la espalda = punto más bajo del tronco (omóplatos), no de piernas ni cabeza
            if (hueso == "Hips" || hueso.StartsWith("Spine")) minYespalda = Mathf.Min(minYespalda, piel[i].y);
        }
        float espaldaBajoCaderas = (hp.y - minYespalda) / escalaPac;
        float despl = ((minE + maxE) / 2f) / escalaPac;
        float largo = Mathf.Ceil(((maxE - minE) / escalaPac + TABLERO_HOLGURA_LARGO) * 10f) / 10f;

        var gSup = new GameObject("SuperficiePaciente"); gSup.transform.SetParent(root.transform, false);
        var tablero = Pieza("wall_base", gSup, Vector3.zero, 0f, 1f);
        tablero.name = "Tablero";
        float sx = largo / WB_X, sy = TABLERO_GRUESO / WB_Y, sz = TABLERO_ANCHO / WB_Z;
        tablero.transform.localScale = new Vector3(sx, sy, sz);
        tablero.transform.localPosition = new Vector3(0f, 0f, WB_Z * sz / 2f);          // centra el tablero (el pivote del pack está en el borde trasero)
        var sup = gSup.AddComponent<SuperficiePaciente>();
        var soS = new SerializedObject(sup);
        soS.FindProperty("raizPaciente").objectReferenceValue = pac.transform;
        soS.FindProperty("caderas").objectReferenceValue = hips;
        soS.FindProperty("espaldaBajoCaderas").floatValue = espaldaBajoCaderas;
        soS.FindProperty("desplazamientoLongitudinal").floatValue = despl;
        soS.FindProperty("giroRespectoPaciente").floatValue = giro;
        soS.FindProperty("visual").objectReferenceValue = tablero.GetComponentInChildren<MeshRenderer>();
        soS.ApplyModifiedPropertiesWithoutUndo();
        sup.Actualizar(); sup.AplicarTinte();

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[SalaRCP] Sala construida: " + RAIZ + " (" + (X_MAX - X_MIN) + " x " + (Z_MAX - Z_MIN) + " x " + ALTO + " m). Tablero " + largo.ToString("0.0") + " x " + TABLERO_ANCHO
                  + " m; espaldaBajoCaderas=" + espaldaBajoCaderas.ToString("0.000") + " desplLongitudinal=" + despl.ToString("0.000") + " giro=" + giro.ToString("0.00") + "°. Entorno viejo desactivado.");
    }

    // Gabinete bajo + gabinete alto encima, contra una pared.
    static void Gabinete(GameObject padre, char pared, float along)
    {
        ContraPared("cupboard_bottom", padre, pared, along, 0f);
        ContraPared("cupboard_top", padre, pared, along, 0.689f * ESCALA);
    }

    /// <summary>Coloca una pieza con su espalda (z local mínimo) contra la pared indicada y su frente hacia el interior.
    /// pared: I = izquierda (x mín), D = derecha (x máx), T = trasera (z mín), F = frontal (z máx). 'along' es la
    /// coordenada a lo largo de esa pared (z en I/D, x en T/F).</summary>
    static GameObject ContraPared(string modelo, GameObject padre, char pared, float along, float y)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CARPETA_PACK + "/" + modelo + ".fbx");
        if (prefab == null) { Debug.LogError("[SalaRCP] FBX no encontrado: " + modelo); return null; }
        float zMin = float.MaxValue;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var b = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                zMin = Mathf.Min(zMin, m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz))).z);
        }
        float adentro = -zMin * ESCALA + 0.01f;                      // separa la espalda de la pared 1 cm
        Vector3 pos; float rot;
        switch (pared)
        {
            case 'I': pos = new Vector3(X_MIN + adentro, y, along); rot = 90f; break;
            case 'D': pos = new Vector3(X_MAX - adentro, y, along); rot = 270f; break;
            case 'T': pos = new Vector3(along, y, Z_MIN + adentro); rot = 0f; break;
            default:  pos = new Vector3(along, y, Z_MAX - adentro); rot = 180f; break;
        }
        return Pieza(modelo, padre, pos, rot, ESCALA);
    }

    static GameObject Hijo(GameObject padre, string nombre)
    {
        var go = new GameObject(nombre); go.transform.SetParent(padre.transform, false); return go;
    }

    static GameObject Pieza(string modelo, GameObject padre, Vector3 pos, float rotY, float escala)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CARPETA_PACK + "/" + modelo + ".fbx");
        if (prefab == null) { Debug.LogError("[SalaRCP] FBX no encontrado: " + modelo); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.transform);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, rotY, 0f); go.transform.localScale = Vector3.one * escala;
        return go;
    }
}
#endif
