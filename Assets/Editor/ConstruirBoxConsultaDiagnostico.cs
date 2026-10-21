#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Construye en SceneDiagnostico el box de consulta (root <c>EntornoBoxConsulta</c>) SOLO con piezas del Hospital
/// Pack que el proyecto ya tiene, y deja al paciente con dos poses (Sentado por defecto / Acostado = estado original
/// con la altura ajustada a la camilla), guardadas en <see cref="PosePaciente"/>. Idempotente: destruye y recrea el
/// entorno y recalcula las poses a partir del estado acostado original (guardado la primera vez).
///
/// No importa ni crea piezas, texturas ni personajes. Pieza que el pack no trae (cortina, silla): no se sustituye.
/// El techo se arma con las mismas losas floor_1 puestas boca abajo (el pack no trae pieza de techo).
/// Aborta si la escena activa no es SceneDiagnostico. NO toca SceneSimulador, MontarSalaHospitalPack ni
/// ConstruirArranqueSeccion2.
///
/// Menú: Tools/Razonamiento Clínico/Construir box de consulta (SceneDiagnostico)
/// </summary>
public static class ConstruirBoxConsultaDiagnostico
{
    public const string RAIZ = "EntornoBoxConsulta";
    const string CARPETA_PACK = "Assets/Enviroment/HospitalAssets/Models/fbx (Unity Rotated)";

    // ── Geometría (m). Todo el pack a la MISMA escala para que mantenga sus proporciones ────────────────────────────
    // Escala 1,25: módulo 2,5 m y altura de pared 2,5 m. Con la escala natural (2,0 m) los paneles head-locked (hasta
    // +0,45 m sobre los ojos) atravesarían el techo.
    public const float ESCALA = 1.25f;
    public const float MODULO = 2f * ESCALA;                        // 2,5 m
    public const float X_MIN = -2.5f, X_MAX = 2.5f;                  // 5,0 m
    public const float Z_MIN = -1.5f, Z_MAX = 3.5f;                  // 5,0 m (pared trasera a 1,5 m del jugador)
    public const float ALTO = MODULO;

    // Camilla (bed_2: plana, sin respaldo), a escala propia para que el largo cubra el cuerpo acostado.
    public const float ESCALA_CAMILLA = 1.3f;
    public const float CAMILLA_Z_ACOSTADO = 1.025f;                  // centra la camilla bajo el cuerpo original (z 0,10-1,93)
    public const float DESPL_CAMILLA_SENTADO = 0.55f;                // sentado la camilla avanza: rodillas ~0,55 m del jugador
    const float HOLGURA_ESPALDA = 0.003f;                            // la espalda hunde 3 mm el colchón (contacto)
    const float HUNDIMIENTO_ASIENTO = 0.005f;                        // el asiento hunde 5 mm (muy por debajo de los 2 cm de tolerancia)

    static readonly StaticEditorFlags FLAGS_ESTATICOS =
        StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

    [MenuItem("Tools/Razonamiento Clínico/Construir box de consulta (SceneDiagnostico)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }
        var pac = GameObject.Find("Paciente_Completo");
        var armature = pac != null ? pac.transform.Find("Banana Man/Armature") : null;
        var regiones = pac != null ? pac.transform.Find("Regiones") : null;
        if (pac == null || armature == null || regiones == null || regiones.childCount != 12)
        {
            Debug.LogError("[BoxConsulta] Falta Paciente_Completo/Banana Man/Armature o los 12 anclajes de Regiones.");
            return;
        }

        // ── 1. Estado acostado ORIGINAL (se captura la 1ª vez; después se restaura de lo guardado) ───────────────
        var pose = pac.GetComponent<PosePaciente>();
        if (pose == null) pose = pac.AddComponent<PosePaciente>();
        var so = new SerializedObject(pose);
        var huesos = new List<Transform>(armature.GetComponentsInChildren<Transform>(true));
        var anclajes = new List<Transform>(); foreach (Transform r in regiones) anclajes.Add(r);

        bool yaGuardado = so.FindProperty("acostado.posicionesHuesos").arraySize == huesos.Count;
        Vector3 rootPosOriginal; Quaternion rootRot;
        if (yaGuardado)
        {
            rootPosOriginal = so.FindProperty("posicionRaizAcostadaOriginal").vector3Value;
            rootRot = so.FindProperty("acostado.rotacionRaiz").quaternionValue;
            pac.transform.SetPositionAndRotation(rootPosOriginal, rootRot);
            var pp = so.FindProperty("acostado.posicionesHuesos"); var rr = so.FindProperty("acostado.rotacionesHuesos"); var pa = so.FindProperty("acostado.posicionesAnclajes");
            for (int i = 0; i < huesos.Count; i++) huesos[i].SetLocalPositionAndRotation(pp.GetArrayElementAtIndex(i).vector3Value, rr.GetArrayElementAtIndex(i).quaternionValue);
            for (int i = 0; i < anclajes.Count; i++) anclajes[i].localPosition = pa.GetArrayElementAtIndex(i).vector3Value;
        }
        else
        {
            rootPosOriginal = pac.transform.position; rootRot = pac.transform.rotation;
        }
        var posHuesosAc = new Vector3[huesos.Count]; var rotHuesosAc = new Quaternion[huesos.Count]; var posAncAc = new Vector3[anclajes.Count];
        for (int i = 0; i < huesos.Count; i++) { posHuesosAc[i] = huesos[i].localPosition; rotHuesosAc[i] = huesos[i].localRotation; }
        for (int i = 0; i < anclajes.Count; i++) posAncAc[i] = anclajes[i].localPosition;

        // Anclaje -> hueso al que se "pega" (offset en el espacio local del hueso, para seguirlo en cualquier pose).
        string[] huesoDeAnclaje = new string[anclajes.Count];
        for (int i = 0; i < anclajes.Count; i++) huesoDeAnclaje[i] = HuesoParaAnclaje(i, anclajes[i].name);
        var porNombre = new Dictionary<string, Transform>(); foreach (var h in huesos) porNombre[h.name] = h;
        var offLocal = new Vector3[anclajes.Count];
        for (int i = 0; i < anclajes.Count; i++) { var b = porNombre[huesoDeAnclaje[i]]; offLocal[i] = Quaternion.Inverse(b.rotation) * (anclajes[i].position - b.position); }

        // ── 2. Entorno ───────────────────────────────────────────────────────────────────────────────────────────
        var viejo = GameObject.Find(RAIZ); if (viejo != null) Object.DestroyImmediate(viejo);
        var root = new GameObject(RAIZ);
        var gSuelo = Hijo(root, "Suelo"); var gTecho = Hijo(root, "Techo"); var gParedes = Hijo(root, "Paredes");
        var gMob = Hijo(root, "Mobiliario"); var gLuces = Hijo(root, "Luces");

        float[] cx = { -MODULO / 2f, MODULO / 2f };                           // -1,25 / +1,25
        float[] cz = { Z_MIN + MODULO / 2f, Z_MIN + 1.5f * MODULO };         // -0,25 / +2,25
        foreach (float x in cx) foreach (float z in cz)
        {
            Pieza("floor_1", gSuelo, new Vector3(x, 0f, z), 0f, ESCALA);
            var techo = Pieza("floor_1", gTecho, new Vector3(x, ALTO, z), 0f, ESCALA);   // losa boca abajo = techo
            if (techo != null) techo.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        }
        // Paredes: la cara interior mira hacia +Z local (como en la sala de RCP). Trasera con puerta, derecha con ventana.
        Pieza("wall_1",    gParedes, new Vector3(cx[0], 0f, Z_MIN), 0f, ESCALA);
        Pieza("wall_door", gParedes, new Vector3(cx[1], 0f, Z_MIN), 0f, ESCALA);
        Pieza("wall_1", gParedes, new Vector3(cx[0], 0f, Z_MAX), 180f, ESCALA);
        Pieza("wall_1", gParedes, new Vector3(cx[1], 0f, Z_MAX), 180f, ESCALA);
        Pieza("wall_1", gParedes, new Vector3(X_MIN, 0f, cz[0]), 90f, ESCALA);
        Pieza("wall_1", gParedes, new Vector3(X_MIN, 0f, cz[1]), 90f, ESCALA);
        Pieza("wall_1",      gParedes, new Vector3(X_MAX, 0f, cz[0]), 270f, ESCALA);
        Pieza("wall_window", gParedes, new Vector3(X_MAX, 0f, cz[1]), 270f, ESCALA);

        // Mobiliario SOLO contra las paredes y fuera del pasillo de la camilla (|x| > 1,4 m o junto a la pared del fondo/trasera).
        // Pared izquierda: dos gabinetes bajos con sus gabinetes altos encima (como en la sala de RCP).
        float zc = 0.1f;
        foreach (float z in new[] { zc, zc + 1.0f })
        {
            Pieza("cupboard_bottom", gMob, new Vector3(X_MIN, 0f, z), 90f, ESCALA);
            Pieza("cupboard_top", gMob, new Vector3(X_MIN, 0.689f * ESCALA, z), 90f, ESCALA);
        }
        // Pared derecha: monitor/desfibrilador y carro de equipo (respaldo contra la pared).
        Pieza("machine_1", gMob, new Vector3(X_MAX - 0.22f * ESCALA, 0f, 0.0f), 270f, ESCALA);
        Pieza("machine_2", gMob, new Vector3(X_MAX - 0.25f * ESCALA, 0f, 0.9f), 270f, ESCALA);
        // Rincón del fondo: mesa de instrumental con bandeja.
        Pieza("tray_1", gMob, new Vector3(1.9f, 0f, Z_MAX - 0.1f * ESCALA - 0.05f), 180f, ESCALA);
        Pieza("tray_2", gMob, new Vector3(1.9f, 0.568f * ESCALA, Z_MAX - 0.1f * ESCALA - 0.05f), 180f, ESCALA);
        // Pared trasera, junto a la esquina izquierda: un gabinete bajo (fuera de lo que mira el jugador).
        Pieza("cupboard_bottom", gMob, new Vector3(-1.6f, 0f, Z_MIN), 0f, ESCALA);

        // Camilla (no estática: PosePaciente la mueve al cambiar de pose)
        var camilla = Pieza("bed_2", root, new Vector3(0f, 0f, CAMILLA_Z_ACOSTADO), 0f, ESCALA_CAMILLA);
        camilla.name = "Camilla";

        // Luces: 1 direccional + 2 puntuales de techo, tiempo real, sin sombras.
        var gl = new GameObject("Luz_Principal"); gl.transform.SetParent(gLuces.transform, false);
        gl.transform.rotation = Quaternion.Euler(62f, -25f, 0f);
        var lp = gl.AddComponent<Light>(); lp.type = LightType.Directional; lp.shadows = LightShadows.None; lp.lightmapBakeType = LightmapBakeType.Realtime;
        var puntuales = new Light[2]; Vector3[] pp2 = { new Vector3(-1.0f, ALTO - 0.3f, 0.6f), new Vector3(1.0f, ALTO - 0.3f, 2.2f) };
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

        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            if (!mr.transform.IsChildOf(camilla.transform)) GameObjectUtility.SetStaticEditorFlags(mr.gameObject, FLAGS_ESTATICOS);

        // ── 3. Camilla: superficie y huella ──────────────────────────────────────────────────────────────────────
        Bounds bc = BoundsMundo(camilla);
        float camTop = bc.max.y;

        // ── 4. Pose ACOSTADO: misma pose, solo altura (la espalda toca el colchón) ───────────────────────────────
        int[] dom; var smr = pac.transform.Find("Banana Man/Body").GetComponent<SkinnedMeshRenderer>();
        pac.transform.SetPositionAndRotation(rootPosOriginal, rootRot);
        Vector3[] piel = PielMundo(smr, out dom);
        float minYespalda = float.MaxValue; for (int i = 0; i < piel.Length; i++) if (Mathf.Abs(piel[i].x) < 0.33f) minYespalda = Mathf.Min(minYespalda, piel[i].y);
        float dyAcostado = (camTop - HOLGURA_ESPALDA) - minYespalda;
        Vector3 rootPosAcostado = rootPosOriginal + new Vector3(0f, dyAcostado, 0f);
        pac.transform.position = rootPosAcostado;

        // ── 5. Pose SENTADO ─────────────────────────────────────────────────────────────────────────────────────
        camilla.transform.position = new Vector3(0f, 0f, CAMILLA_Z_ACOSTADO + DESPL_CAMILLA_SENTADO);
        bc = BoundsMundo(camilla);
        float bordeCercano = bc.min.z;
        Vector3 hips = porNombre["Hips"].position;
        porNombre["Spine 1"].RotateAround(hips, Vector3.right, 90f);                                  // tronco erguido
        porNombre["Left Leg"].RotateAround(porNombre["Left Leg"].position, Vector3.right, 90f);       // piernas colgando
        porNombre["Right Leg"].RotateAround(porNombre["Right Leg"].position, Vector3.right, 90f);
        foreach (var lado in new[] { "Left", "Right" })                                               // brazos sobre los muslos (IK de 2 huesos)
        {
            var A = porNombre[lado + " Arm"]; var Fo = porNombre[lado + " Forearm"]; var H = porNombre[lado + " Hand"];
            var Th = porNombre[lado + " Thigh"]; var Kn = porNombre[lado + " Leg"];
            float L1 = Vector3.Distance(A.position, Fo.position), L2 = Vector3.Distance(Fo.position, H.position);
            Vector3 S = A.position; Vector3 T = Vector3.Lerp(Th.position, Kn.position, 0.62f) + Vector3.up * 0.075f;
            float d = Mathf.Min(Vector3.Distance(S, T), L1 + L2 - 0.002f);
            Vector3 dir = (T - S).normalized; float a = (L1 * L1 - L2 * L2 + d * d) / (2f * d); float h = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - a * a));
            Vector3 lateral = lado == "Left" ? Vector3.left : Vector3.right;
            Vector3 polo = (lateral * 0.6f + Vector3.back * 0.2f + Vector3.down * 0.2f).normalized;
            Vector3 perp = (polo - Vector3.Dot(polo, dir) * dir).normalized; Vector3 E = S + dir * a + perp * h;
            A.rotation = Quaternion.FromToRotation((Fo.position - A.position).normalized, (E - A.position).normalized) * A.rotation;
            Fo.rotation = Quaternion.FromToRotation((H.position - Fo.position).normalized, (T - Fo.position).normalized) * Fo.rotation;
        }
        pac.transform.RotateAround(porNombre["Hips"].position, Vector3.up, 180f);                     // mira al jugador (-Z)
        Vector3 rodillas = (porNombre["Left Leg"].position + porNombre["Right Leg"].position) / 2f;
        pac.transform.position += new Vector3(-porNombre["Hips"].position.x, 0f, (bordeCercano - 0.105f) - rodillas.z);
        piel = PielMundo(smr, out dom);
        float minAsiento = float.MaxValue; for (int i = 0; i < piel.Length; i++) { string nm = smr.bones[dom[i]].name; if (nm == "Hips" || nm.EndsWith("Thigh")) minAsiento = Mathf.Min(minAsiento, piel[i].y); }
        pac.transform.position += new Vector3(0f, (camTop - HUNDIMIENTO_ASIENTO) - minAsiento, 0f);

        // anclajes pegados a sus huesos (misma pose relativa que acostado)
        var posAncSe = new Vector3[anclajes.Count];
        for (int i = 0; i < anclajes.Count; i++)
        {
            var b = porNombre[huesoDeAnclaje[i]];
            Vector3 w = b.position + b.rotation * offLocal[i];
            posAncSe[i] = pac.transform.InverseTransformPoint(w);
        }
        var posHuesosSe = new Vector3[huesos.Count]; var rotHuesosSe = new Quaternion[huesos.Count];
        for (int i = 0; i < huesos.Count; i++) { posHuesosSe[i] = huesos[i].localPosition; rotHuesosSe[i] = huesos[i].localRotation; }
        Vector3 rootPosSe = pac.transform.position; Quaternion rootRotSe = pac.transform.rotation;
        Vector3 camillaSe = camilla.transform.position;

        // ── 6. Guardar en PosePaciente ───────────────────────────────────────────────────────────────────────────
        Guardar(so, "acostado", rootPosAcostado, rootRot, posHuesosAc, rotHuesosAc, posAncAc, new Vector3(0f, 0f, CAMILLA_Z_ACOSTADO));
        Guardar(so, "sentado", rootPosSe, rootRotSe, posHuesosSe, rotHuesosSe, posAncSe, camillaSe);
        so.FindProperty("posicionRaizAcostadaOriginal").vector3Value = rootPosOriginal;
        so.FindProperty("pose").enumValueIndex = (int)PosePacienteTipo.Sentado;
        SetRefs(so, "huesos", huesos.ToArray()); SetRefs(so, "anclajes", anclajes.ToArray());
        so.FindProperty("camilla").objectReferenceValue = camilla.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        pose.Aplicar(PosePacienteTipo.Sentado);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[BoxConsulta] Entorno y poses construidos. camTop=" + camTop.ToString("0.000") + " dyAcostado=" + dyAcostado.ToString("0.000") + " bordeCercano(sentado)=" + bordeCercano.ToString("0.000"));
    }

    // Anclaje -> hueso (por índice/nombre de los 12 marcadores, en el orden de Regiones).
    static string HuesoParaAnclaje(int i, string nombre)
    {
        switch (i)
        {
            case 0: return "Neck";        // Cabeza y cuello
            case 1: return "Spine 3";     // Tórax anterior
            case 2: return "Spine 3";     // Tórax posterior
            case 3: return "Spine 3";     // Precordio
            case 4: return "Spine 1";     // Abdomen
            case 5: return "Left Forearm";
            case 6: return "Right Forearm";
            case 7: return "Left Thigh";
            case 8: return "Right Thigh";
            case 9: return "Head";        // Piel y mucosas (cara)
            case 10: return "Left Hand";  // Piel y mucosas (mano)
            default: return "Right Hand";
        }
    }

    static void Guardar(SerializedObject so, string campo, Vector3 posRaiz, Quaternion rotRaiz, Vector3[] pH, Quaternion[] rH, Vector3[] pA, Vector3 pCam)
    {
        so.FindProperty(campo + ".posicionRaiz").vector3Value = posRaiz;
        so.FindProperty(campo + ".rotacionRaiz").quaternionValue = rotRaiz;
        so.FindProperty(campo + ".posicionCamilla").vector3Value = pCam;
        var a = so.FindProperty(campo + ".posicionesHuesos"); a.arraySize = pH.Length; for (int i = 0; i < pH.Length; i++) a.GetArrayElementAtIndex(i).vector3Value = pH[i];
        var b = so.FindProperty(campo + ".rotacionesHuesos"); b.arraySize = rH.Length; for (int i = 0; i < rH.Length; i++) b.GetArrayElementAtIndex(i).quaternionValue = rH[i];
        var c = so.FindProperty(campo + ".posicionesAnclajes"); c.arraySize = pA.Length; for (int i = 0; i < pA.Length; i++) c.GetArrayElementAtIndex(i).vector3Value = pA[i];
    }

    static void SetRefs(SerializedObject so, string campo, Object[] refs)
    {
        var p = so.FindProperty(campo); p.arraySize = refs.Length;
        for (int i = 0; i < refs.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = refs[i];
    }

    static GameObject Hijo(GameObject padre, string nombre)
    {
        var go = new GameObject(nombre); go.transform.SetParent(padre.transform, false); return go;
    }

    static GameObject Pieza(string modelo, GameObject padre, Vector3 pos, float rotY, float escala)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CARPETA_PACK + "/" + modelo + ".fbx");
        if (prefab == null) { Debug.LogError("[BoxConsulta] FBX no encontrado: " + modelo); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre.transform);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, rotY, 0f); go.transform.localScale = Vector3.one * escala;
        return go;
    }

    public static Bounds BoundsMundo(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true); var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b;
    }

    /// <summary>Vértices del Body en el mundo según la pose ACTUAL de los huesos (skinning manual; BakeMesh no sirve con este rig).
    /// <paramref name="dominante"/> = índice (en smr.bones) del hueso de mayor peso de cada vértice.</summary>
    public static Vector3[] PielMundo(SkinnedMeshRenderer smr, out int[] dominante)
    {
        var mesh = smr.sharedMesh; var verts = mesh.vertices; var bw = mesh.boneWeights; var bind = mesh.bindposes; var bones = smr.bones;
        var mats = new Matrix4x4[bones.Length]; for (int i = 0; i < bones.Length; i++) mats[i] = bones[i].localToWorldMatrix * bind[i];
        var W = new Vector3[verts.Length]; dominante = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var b = bw[i];
            Vector3 p = b.weight0 * (Vector3)mats[b.boneIndex0].MultiplyPoint3x4(verts[i]);
            if (b.weight1 > 0) p += b.weight1 * (Vector3)mats[b.boneIndex1].MultiplyPoint3x4(verts[i]);
            if (b.weight2 > 0) p += b.weight2 * (Vector3)mats[b.boneIndex2].MultiplyPoint3x4(verts[i]);
            if (b.weight3 > 0) p += b.weight3 * (Vector3)mats[b.boneIndex3].MultiplyPoint3x4(verts[i]);
            W[i] = p; dominante[i] = b.boneIndex0;
        }
        return W;
    }
}
#endif
