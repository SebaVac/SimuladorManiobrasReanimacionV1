#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 5 — Anamnesis (GameObject <c>Panel_Anamnesis</c>).
///
/// Estética teal diegética (misma paleta que los demás paneles del módulo). Head-locked
/// (AnclajeCabeza en Raiz, misma posición que Paneles 1/2/7 — el paciente está oculto
/// durante Anamnesis). Historial acumulativo scrolleable (ScrollRect World Space +
/// RectMask2D + ContentSizeFitter sobre un único TMP que crece; no había patrón de
/// scroll reutilizable en el proyecto). Se activa solo en EstadoCasoClinico.Anamnesis
/// (lógica en PanelAnamnesis).
///
/// Idempotente: borra la versión previa antes de reconstruir.
/// Menú: Tools/Razonamiento Clínico/Construir Panel 5 (Anamnesis)
/// </summary>
public static class ConstruirPanel5Anamnesis
{
    static readonly Color COLOR_PANEL   = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_HISTORIAL = new Color(0.02f, 0.06f, 0.07f, 0.85f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO   = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 5 (Anamnesis)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_Anamnesis");

        var gestor      = Object.FindObjectOfType<GestorCasoClinico>();
        var interaccion = Object.FindObjectOfType<InteraccionPaciente>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null || interaccion == null)
            Debug.LogWarning("[Panel5] No se encontró GestorCasoClinico / InteraccionPaciente.");

        var ctrl = new GameObject("Panel_Anamnesis");
        var comp = ctrl.AddComponent<PanelAnamnesis>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        // Alto 660 (era 620): +40 simétrico solo para dejar hueco debajo de
        // BotonContinuar para el texto de ayuda del estado bloqueado (2026-09-16).
        // No mueve ningún elemento existente, solo agranda el margen.
        var canvas = NuevoCanvas("Canvas", raiz.transform, 700f, 660f);
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "ANAMNESIS", 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 272f), new Vector2(640f, 44f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 246f), 640f, COLOR_ACENTO);

        // ── Historial scrolleable ─────────────────────────────────────────
        TMP_Text textoHistorial;
        var scroll = ConstruirScroll(canvas.transform, new Vector2(0f, 40f), new Vector2(640f, 330f),
                                     out textoHistorial);

        // Sin glifos ▲/▼: la fuente TMP por defecto (LiberationSans SDF) no los trae
        // en su tabla primaria y el texto "ANTERIORES"/"RECIENTES" ya indica la función.
        var scrollArriba = BotonSimple(canvas.transform, "BotonScrollArriba", "ANTERIORES",
              new Vector2(-78f, -152f), new Vector2(150f, 40f), 18);
        var scrollAbajo  = BotonSimple(canvas.transform, "BotonScrollAbajo", "RECIENTES",
              new Vector2(78f, -152f), new Vector2(150f, 40f), 18);

        // ── Interacción ───────────────────────────────────────────────────
        var botonPreguntar = BotonSimple(canvas.transform, "BotonPreguntar", "ESCRIBIR UNA PREGUNTA",
              new Vector2(0f, -206f), new Vector2(360f, 48f), 22);

        var botonContinuar = BotonSimple(canvas.transform, "BotonContinuar", "CONTINUAR A EXÁMENES COMPLEMENTARIOS",
              new Vector2(0f, -262f), new Vector2(580f, 48f), 20);
        var fondoContinuar = botonContinuar.GetComponent<Image>();

        // Ayuda visible solo mientras Continuar está bloqueado (hueco nuevo del canvas).
        var textoAyuda = Texto(canvas.transform, "TextoAyudaBloqueado", "", 16, FontStyles.Italic,
              COLOR_TENUE, TextAlignmentOptions.Center, new Vector2(0f, -308f), new Vector2(580f, 26f));

        // ── Cableado ──────────────────────────────────────────────────────
        Set(comp, "raiz", raiz);
        Set(comp, "scroll", scroll);
        Set(comp, "textoHistorial", textoHistorial);
        Set(comp, "botonScrollArriba", scrollArriba);
        Set(comp, "botonScrollAbajo", scrollAbajo);
        Set(comp, "botonPreguntar", botonPreguntar);
        Set(comp, "botonContinuar", botonContinuar);
        Set(comp, "fondoBotonContinuar", fondoContinuar);
        Set(comp, "textoAyudaBloqueado", textoAyuda);
        Set(comp, "gestorCaso", gestor);
        Set(comp, "interaccionPaciente", interaccion);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel5] Panel_Anamnesis construido y guardado en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Scroll World Space (ScrollRect + RectMask2D + ContentSizeFitter)
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

        // Contenido = el propio TMP, anclado arriba y creciendo hacia abajo.
        var goTxt = new GameObject("TextoHistorial", typeof(RectTransform), typeof(ContentSizeFitter));
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
        sr.scrollSensitivity = 0f;   // no hay rueda de mouse en VR; se scrollea por botones / auto
        sr.horizontalScrollbar = null;
        sr.verticalScrollbar = null;

        texto = t;
        return sr;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers UI (mismos que ConstruirPanel4Hallazgos)
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
        if (go == null) Debug.LogWarning("[Panel5] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel5] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel5] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
