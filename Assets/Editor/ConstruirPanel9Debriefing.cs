#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 9 — Debriefing (GameObject
/// <c>Panel_Debriefing</c>).
///
/// Estética teal diegética (consistente con el resto del módulo — a diferencia
/// del Panel 7, que rompe estilo a propósito). Head-locked, mismo
/// distancia/alturaRelativa que la mayoría de los paneles principales (0.60 /
/// -0.05): a diferencia del ícono de pausa y la hoja de notas, este panel NO
/// necesita esquivar a los demás porque nunca coexiste con ellos — Debriefing
/// es un estado exclusivo, todos los demás paneles del módulo ya están
/// ocultos para cuando este se muestra.
///
/// Idempotente. Menú: Tools/Razonamiento Clínico/Construir Panel 9 (Debriefing)
/// </summary>
public static class ConstruirPanel9Debriefing
{
    static readonly Color COLOR_PANEL     = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_HISTORIAL = new Color(0.02f, 0.06f, 0.07f, 0.85f);
    static readonly Color COLOR_TARJETA   = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO     = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE     = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO    = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 9 (Debriefing)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_Debriefing");

        var gestor = Object.FindObjectOfType<GestorCasoClinico>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null)
            Debug.LogWarning("[Panel9] No se encontró GestorCasoClinico en la escena.");

        var ctrl = new GameObject("Panel_Debriefing");
        var comp = ctrl.AddComponent<PanelDebriefing>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 760f, 680f);
        Fondo(canvas.transform, COLOR_PANEL);

        // Jerarquía visual (2026-09-23): el diagnóstico es el titular del
        // resumen (grande, arriba) — el puntaje bajó de tamaño y se movió
        // debajo, con la aclaración de que el manejo terapéutico está
        // pendiente (siempre puntúa 0 hoy; sin esto se leía como "el sistema
        // está roto" en validación con usuarios). Ver PanelDebriefing.Poblar().
        Texto(canvas.transform, "Titulo", "DEBRIEFING", 24, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(700f, 40f));
        Linea(canvas.transform, "Regla1", new Vector2(0f, 276f), 700f, COLOR_ACENTO);

        var diagnostico = Texto(canvas.transform, "TextoDiagnostico", "Diagnóstico: —", 32, FontStyles.Bold,
              COLOR_TEXTO, TextAlignmentOptions.Center, new Vector2(0f, 225f), new Vector2(700f, 70f));

        var puntaje = Texto(canvas.transform, "TextoPuntaje", "Puntaje: — / —", 18, FontStyles.Normal,
              COLOR_TENUE, TextAlignmentOptions.Center, new Vector2(0f, 150f), new Vector2(700f, 60f));
        Linea(canvas.transform, "Regla2", new Vector2(0f, 110f), 700f, COLOR_ACENTO);

        TMP_Text textoResumen;
        var scroll = ConstruirScroll(canvas.transform, new Vector2(0f, -40f), new Vector2(680f, 280f),
                                     out textoResumen);

        var scrollArriba = BotonSimple(canvas.transform, "BotonScrollArriba", "ANTERIORES",
              new Vector2(-110f, -190f), new Vector2(140f, 34f), 16);
        var scrollAbajo  = BotonSimple(canvas.transform, "BotonScrollAbajo", "RECIENTES",
              new Vector2(110f, -190f), new Vector2(140f, 34f), 16);

        var botonReintentar = BotonSimple(canvas.transform, "BotonReintentar", "REINTENTAR ESTE CASO",
              new Vector2(-180f, -250f), new Vector2(330f, 54f), 18);
        // Vuelve a PanelSeleccionSubmodo (Panel 2), no a SceneInicio — renombrado
        // para no confundirse con "Salir al menú principal" del menú de pausa,
        // que sí sigue yendo a SceneInicio (GestorMenuPausa.VolverAlMenuPrincipal).
        var botonVolver = BotonSimple(canvas.transform, "BotonVolver", "VOLVER A SELECCIÓN DE CASO",
              new Vector2(180f, -250f), new Vector2(330f, 54f), 18);

        Set(comp, "raiz", raiz);
        Set(comp, "textoPuntaje", puntaje);
        Set(comp, "textoDiagnostico", diagnostico);
        Set(comp, "scroll", scroll);
        Set(comp, "textoResumen", textoResumen);
        Set(comp, "botonScrollArriba", scrollArriba);
        Set(comp, "botonScrollAbajo", scrollAbajo);
        Set(comp, "botonReintentar", botonReintentar);
        Set(comp, "botonVolver", botonVolver);
        Set(comp, "gestorCaso", gestor);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel9] Panel_Debriefing construido y guardado en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Scroll World Space (mismo patrón que los demás paneles del módulo)
    // ═══════════════════════════════════════════════════════════════════════

    static ScrollRect ConstruirScroll(Transform padre, Vector2 pos, Vector2 size, out TMP_Text texto)
    {
        var go = new GameObject("Resumen", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = COLOR_HISTORIAL;
        img.raycastTarget = false;

        var goTxt = new GameObject("TextoResumen", typeof(RectTransform), typeof(ContentSizeFitter));
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
        t.fontSize = 19;
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
    //  HELPERS DE UI
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
        if (go == null) Debug.LogWarning("[Panel9] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel9] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel9] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
