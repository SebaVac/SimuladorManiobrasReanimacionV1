#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 6 — Exámenes Complementarios
/// (GameObject <c>Panel_ExamenesComplementarios</c>).
///
/// Estética teal diegética. Head-locked (AnclajeCabeza en Raiz, misma posición que
/// Paneles 1/2/5/7). Lista de exámenes DINÁMICA: el builder deja un contenedor con
/// VerticalLayoutGroup + un botón-template desactivado; PanelExamenesComplementarios
/// instancia un botón por examen del caso en runtime. Área de resultados acumulativos
/// con el mismo patrón de scroll del Panel 5 (ScrollRect + RectMask2D + ContentSizeFitter
/// sobre un TMP que crece). Se activa solo en EstadoCasoClinico.ExamenesComplementarios.
///
/// Idempotente. Menú: Tools/Razonamiento Clínico/Construir Panel 6 (Exámenes)
/// </summary>
public static class ConstruirPanel6Examenes
{
    static readonly Color COLOR_PANEL     = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_HISTORIAL = new Color(0.02f, 0.06f, 0.07f, 0.85f);
    static readonly Color COLOR_TARJETA   = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO     = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE     = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO    = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 6 (Exámenes)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_ExamenesComplementarios");

        var gestor      = Object.FindObjectOfType<GestorCasoClinico>();
        var interaccion = Object.FindObjectOfType<InteraccionPaciente>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null || interaccion == null)
            Debug.LogWarning("[Panel6] No se encontró GestorCasoClinico / InteraccionPaciente.");

        var ctrl = new GameObject("Panel_ExamenesComplementarios");
        var comp = ctrl.AddComponent<PanelExamenesComplementarios>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 760f, 660f);
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "EXÁMENES COMPLEMENTARIOS", 26, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 292f), new Vector2(720f, 42f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 266f), 720f, COLOR_ACENTO);

        // ── Columna izquierda: lista dinámica de exámenes ─────────────────
        Texto(canvas.transform, "LblDisponibles", "DISPONIBLES", 16, FontStyles.Bold, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(-185f, 230f), new Vector2(320f, 24f));

        var contenedor = new GameObject("ContenedorExamenes", typeof(RectTransform));
        contenedor.transform.SetParent(canvas.transform, false);
        var rtCont = (RectTransform)contenedor.transform;
        rtCont.anchoredPosition = new Vector2(-185f, 30f);
        rtCont.sizeDelta = new Vector2(320f, 380f);
        var vlg = contenedor.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;  vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

        var prefab = CrearBotonExamenTemplate(canvas.transform);

        // ── Columna derecha: resultados acumulativos scrolleables ─────────
        Texto(canvas.transform, "LblResultados", "RESULTADOS", 16, FontStyles.Bold, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(175f, 230f), new Vector2(360f, 24f));

        TMP_Text textoResultados;
        var scroll = ConstruirScroll(canvas.transform, new Vector2(175f, 35f), new Vector2(360f, 370f),
                                     out textoResultados);

        var scrollArriba = BotonSimple(canvas.transform, "BotonScrollArriba", "ANTERIORES",
              new Vector2(105f, -175f), new Vector2(140f, 34f), 16);
        var scrollAbajo  = BotonSimple(canvas.transform, "BotonScrollAbajo", "RECIENTES",
              new Vector2(250f, -175f), new Vector2(140f, 34f), 16);

        // ── Continuar ─────────────────────────────────────────────────────
        var botonContinuar = BotonSimple(canvas.transform, "BotonContinuar", "CONTINUAR A REGISTRO DE DIAGNÓSTICO",
              new Vector2(0f, -295f), new Vector2(560f, 46f), 20);
        var fondoContinuar = botonContinuar.GetComponent<Image>();

        // ── Cableado ──────────────────────────────────────────────────────
        Set(comp, "raiz", raiz);
        Set(comp, "contenedorExamenes", contenedor.transform);
        Set(comp, "prefabBotonExamen", prefab);
        Set(comp, "scroll", scroll);
        Set(comp, "textoResultados", textoResultados);
        Set(comp, "botonScrollArriba", scrollArriba);
        Set(comp, "botonScrollAbajo", scrollAbajo);
        Set(comp, "botonContinuar", botonContinuar);
        Set(comp, "fondoBotonContinuar", fondoContinuar);
        Set(comp, "gestorCaso", gestor);
        Set(comp, "interaccionPaciente", interaccion);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel6] Panel_ExamenesComplementarios construido y guardado en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Botón-template de examen (desactivado; se clona en runtime)
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject CrearBotonExamenTemplate(Transform padre)
    {
        var go = new GameObject("PrefabBotonExamen", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(300f, 48f);
        go.GetComponent<Image>().color = COLOR_TARJETA;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 48f;
        le.minHeight = 40f;

        var fb  = go.AddComponent<BotonMenuFeedback>();
        var sel = go.AddComponent<SeleccionableToque>();
        Set(sel, "feedbackVisual", fb);
        SetFloat(sel, "profundidadToque", 0.05f);

        Texto(go.transform, "Etiqueta", "(examen)", 17, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 40f));

        go.SetActive(false);
        return go;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Scroll (idéntico a ConstruirPanel5Anamnesis)
    // ═══════════════════════════════════════════════════════════════════════

    static ScrollRect ConstruirScroll(Transform padre, Vector2 pos, Vector2 size, out TMP_Text texto)
    {
        var go = new GameObject("Historial", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = COLOR_HISTORIAL;
        img.raycastTarget = false;

        var goTxt = new GameObject("TextoResultados", typeof(RectTransform), typeof(ContentSizeFitter));
        goTxt.transform.SetParent(go.transform, false);
        var rtTxt = (RectTransform)goTxt.transform;
        rtTxt.anchorMin = new Vector2(0f, 1f);
        rtTxt.anchorMax = new Vector2(1f, 1f);
        rtTxt.pivot     = new Vector2(0.5f, 1f);
        rtTxt.offsetMin = new Vector2(16f, 0f);
        rtTxt.offsetMax = new Vector2(-16f, 0f);
        rtTxt.anchoredPosition = new Vector2(0f, -12f);

        var t = goTxt.AddComponent<TextMeshProUGUI>();
        t.text = string.Empty;
        t.fontSize = 18;
        t.color = COLOR_TEXTO;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = true;
        t.richText = true;
        t.raycastTarget = false;

        var csf = goTxt.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        var sr = go.GetComponent<ScrollRect>();
        sr.content = rtTxt;
        sr.viewport = rt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = false;
        sr.scrollSensitivity = 0f;
        sr.horizontalScrollbar = null;
        sr.verticalScrollbar = null;

        texto = t;
        return sr;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers UI
    // ═══════════════════════════════════════════════════════════════════════

    static Canvas NuevoCanvas(string nombre, Transform padre, float w, float h)
    {
        var go = new GameObject(nombre, typeof(Canvas));
        go.transform.SetParent(padre, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        go.transform.localScale = Vector3.one * 0.001f;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return c;
    }

    static Image Fondo(Transform padre, Color color)
    {
        var go = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        go.transform.SetAsFirstSibling();
        return go.GetComponent<Image>();
    }

    static void Linea(Transform padre, string nombre, Vector2 pos, float ancho, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(ancho, 2f);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
    }

    static TextMeshProUGUI Texto(Transform padre, string nombre, string txt, float size,
                                 FontStyles estilo, Color color, TextAlignmentOptions align,
                                 Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt;
        t.fontSize = size;
        t.fontStyle = estilo;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    static SeleccionableToque BotonSimple(Transform padre, string nombre, string etiqueta,
                                          Vector2 pos, Vector2 size, float tamTexto)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = COLOR_TARJETA;

        var fb  = go.AddComponent<BotonMenuFeedback>();
        var sel = go.AddComponent<SeleccionableToque>();
        Set(sel, "feedbackVisual", fb);
        SetFloat(sel, "profundidadToque", 0.05f);

        Texto(go.transform, "Etiqueta", etiqueta, tamTexto, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(16f, 8f));
        return sel;
    }

    // ── Helpers reflexión ─────────────────────────────────────────────────

    static void Limpiar(params string[] nombres)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (var go in escena.GetRootGameObjects())
            foreach (var n in nombres)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
    }

    static Transform BuscarPorRuta(string ruta)
    {
        var go = GameObject.Find(ruta);
        if (go == null) Debug.LogWarning("[Panel6] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel6] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel6] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
