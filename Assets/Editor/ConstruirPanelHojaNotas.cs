#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel de Hoja de Notas (GameObject
/// <c>Panel_HojaNotas</c>).
///
/// Posición auditada con AABB real contra los 7 paneles + BarraNavegacionCaso
/// (2026-09-22) para no repetir el problema del ícono de pausa: por debajo de
/// TODO lo demás (mismo criterio que el ícono, que quedó en alturaRelativa
/// -0.52), del lado DERECHO del jugador (el ícono quedó a la izquierda,
/// desplazamientoLateral -0.25) — distancia=0.6, alturaRelativa=-0.68,
/// desplazamientoLateral=+0.20. Overlapea solo con PanelSeleccionSubmodo, que
/// nunca coexiste (la hoja de notas, igual que el ícono, solo está habilitada
/// en las 4 fases interactivas — ver PuenteVisibilidadPausa).
///
/// Idempotente: borra la versión previa antes de reconstruir.
/// Menú: Tools/Razonamiento Clínico/Construir Panel Hoja de Notas
/// </summary>
public static class ConstruirPanelHojaNotas
{
    static readonly Color COLOR_PANEL     = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_HISTORIAL = new Color(0.02f, 0.06f, 0.07f, 0.85f);
    static readonly Color COLOR_TARJETA   = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO     = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_ACENTO    = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel Hoja de Notas")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_HojaNotas");

        var gestor = Object.FindObjectOfType<GestorCasoClinico>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null)
            Debug.LogWarning("[PanelHojaNotas] No se encontró GestorCasoClinico.");

        var ctrl = new GameObject("Panel_HojaNotas");
        var comp = ctrl.AddComponent<PanelHojaNotas>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.68f);
        SetFloat(anclaje, "desplazamientoLateral", 0.20f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 460f, 380f);
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "NOTAS", 24, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 160f), new Vector2(420f, 34f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 140f), 420f, COLOR_ACENTO);

        TMP_Text textoNotas;
        var scroll = ConstruirScroll(canvas.transform, new Vector2(0f, 10f), new Vector2(420f, 180f),
                                     out textoNotas);

        var scrollArriba = BotonSimple(canvas.transform, "BotonScrollArriba", "ANTERIORES",
              new Vector2(-65f, -110f), new Vector2(120f, 32f), 14);
        var scrollAbajo  = BotonSimple(canvas.transform, "BotonScrollAbajo", "RECIENTES",
              new Vector2(65f, -110f), new Vector2(120f, 32f), 14);

        var botonEscribir = BotonSimple(canvas.transform, "BotonEscribir", "ESCRIBIR NOTA",
              new Vector2(0f, -155f), new Vector2(360f, 40f), 18);

        Set(comp, "raiz", raiz);
        Set(comp, "scroll", scroll);
        Set(comp, "textoNotas", textoNotas);
        Set(comp, "botonScrollArriba", scrollArriba);
        Set(comp, "botonScrollAbajo", scrollAbajo);
        Set(comp, "botonEscribir", botonEscribir);
        Set(comp, "gestorCaso", gestor);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[PanelHojaNotas] Panel_HojaNotas construido y guardado en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Scroll World Space (idéntico al patrón de ConstruirPanel5Anamnesis)
    // ═══════════════════════════════════════════════════════════════════════

    static ScrollRect ConstruirScroll(Transform padre, Vector2 pos, Vector2 size, out TMP_Text texto)
    {
        var go = new GameObject("Notas", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = COLOR_HISTORIAL;
        img.raycastTarget = false;

        var goTxt = new GameObject("TextoNotas", typeof(RectTransform), typeof(ContentSizeFitter));
        goTxt.transform.SetParent(go.transform, false);
        var rtTxt = (RectTransform)goTxt.transform;
        rtTxt.anchorMin = new Vector2(0f, 1f);
        rtTxt.anchorMax = new Vector2(1f, 1f);
        rtTxt.pivot     = new Vector2(0.5f, 1f);
        rtTxt.offsetMin = new Vector2(14f, 0f);
        rtTxt.offsetMax = new Vector2(-14f, 0f);
        rtTxt.anchoredPosition = new Vector2(0f, -10f);

        var t = goTxt.AddComponent<TextMeshProUGUI>();
        t.text = string.Empty;
        t.fontSize = 17;
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
    //  HELPERS DE UI (mismos que ConstruirPanel5Anamnesis)
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
        if (go == null) Debug.LogWarning("[PanelHojaNotas] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[PanelHojaNotas] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[PanelHojaNotas] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
