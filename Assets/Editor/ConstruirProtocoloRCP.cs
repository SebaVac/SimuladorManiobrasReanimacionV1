#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Protocolo de estudio v1.1.0 en SceneSimulador (Parte A):
///   - <c>Panel_IntroduccionRCP</c>: panel de introducción (<see cref="PanelIntroduccionRCP"/>) con campo de código y
///     botón «Comenzar», anclado a la cabeza a 0,65 m como el panel de resultados (nunca coinciden en el tiempo: la
///     introducción solo se ve antes de calibrar y los resultados al terminar una sesión).
///   - Amplía el <c>Panel_ResultadosRCP</c> YA existente (no lo reconstruye): línea «Resultados guardados», cierre
///     tras la última sesión y las referencias nuevas de <see cref="PanelResultadosRCP"/>.
/// Estética teal diegética, igual que el resto del módulo. Idempotente: destruye y recrea solo lo que añade.
/// NO toca SceneInicio ni SceneDiagnostico (aborta si la escena activa no es SceneSimulador) y NO reconstruye ningún
/// otro panel (no llama a ConstruirPanelResultadosRCP).
///
/// Menú: Tools/Simulador RCP/Construir introducción y protocolo
/// </summary>
public static class ConstruirProtocoloRCP
{
    static readonly Color COLOR_PANEL   = new Color(0.03f, 0.09f, 0.11f, 0.94f);
    static readonly Color COLOR_OPACO   = new Color(0.03f, 0.09f, 0.11f, 1f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO   = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);
    static readonly Color COLOR_AVISO   = new Color(0.95f, 0.85f, 0.15f, 1f);

    [MenuItem("Tools/Simulador RCP/Construir introducción y protocolo")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneSimulador")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneSimulador antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        var logica = Object.FindFirstObjectByType<LogicaRCP>();
        var sesion = Object.FindFirstObjectByType<SesionRCP>();
        var menu = Object.FindFirstObjectByType<GestorMenuPausa>();
        var resultados = Object.FindFirstObjectByType<PanelResultadosRCP>(FindObjectsInactive.Include);
        var centerEye = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (logica == null || sesion == null || menu == null || resultados == null || centerEye == null)
        {
            Debug.LogError("[ProtocoloRCP] Falta LogicaRCP, SesionRCP, GestorMenuPausa, Panel_ResultadosRCP o CenterEyeAnchor en la escena.");
            return;
        }

        ExtenderResultados(resultados, menu);
        ConstruirIntroduccion(logica, sesion, menu, resultados, centerEye);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[ProtocoloRCP] Introducción y protocolo construidos y guardados en SceneSimulador.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Panel de resultados: línea de guardado + cierre
    // ═══════════════════════════════════════════════════════════════════════

    static void ExtenderResultados(PanelResultadosRCP panel, GestorMenuPausa menu)
    {
        var canvas = panel.transform.Find("Raiz/Canvas");
        if (canvas == null) { Debug.LogError("[ProtocoloRCP] No se encontró Panel_ResultadosRCP/Raiz/Canvas."); return; }

        foreach (var n in new[] { "TextoGuardado", "Cierre" })
        {
            var viejo = canvas.Find(n);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
        }

        // Línea discreta de guardado, bajo los botones (borde inferior del canvas = -340).
        var guardado = Texto(canvas, "TextoGuardado", "Resultados guardados", 16, FontStyles.Italic, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(0f, -324f), new Vector2(480f, 22f));
        guardado.enableAutoSizing = true; guardado.fontSizeMin = 12f; guardado.fontSizeMax = 16f;
        guardado.gameObject.SetActive(false);

        // Cierre: tapa todo el canvas (fondo opaco) con el agradecimiento y «Volver al menú principal».
        var cierre = new GameObject("Cierre", typeof(RectTransform), typeof(Image));
        cierre.transform.SetParent(canvas, false);
        var rt = (RectTransform)cierre.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var fondo = cierre.GetComponent<Image>(); fondo.color = COLOR_OPACO; fondo.raycastTarget = false;

        Texto(cierre.transform, "Titulo", "ENTREGA FINALIZADA", 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 180f), new Vector2(640f, 46f));
        Linea(cierre.transform, "Regla", new Vector2(0f, 148f), 640f, COLOR_ACENTO);
        var tCierre = Texto(cierre.transform, "TextoCierre", "Terminaste las 3 sesiones. Gracias.", 28, FontStyles.Normal, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 40f), new Vector2(620f, 170f));
        tCierre.enableAutoSizing = true; tCierre.fontSizeMin = 18f; tCierre.fontSizeMax = 28f;
        var bMenu = BotonSimple(cierre.transform, "BotonMenuPrincipal", "VOLVER AL MENÚ PRINCIPAL",
              new Vector2(0f, -150f), new Vector2(480f, 66f), 22);
        cierre.SetActive(false);

        var repetir = Hijo<SeleccionableToque>(canvas, "BotonRepetir");
        Set(panel, "textoEtiquetaRepetir", repetir != null ? repetir.transform.Find("Etiqueta").GetComponent<TMP_Text>() : null);
        Set(panel, "textoGuardado", guardado);
        Set(panel, "cierre", cierre);
        Set(panel, "textoCierre", tCierre);
        Set(panel, "botonMenuPrincipal", bMenu);
        Set(panel, "menuPausa", menu);
        EditorUtility.SetDirty(panel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Panel de introducción
    // ═══════════════════════════════════════════════════════════════════════

    static void ConstruirIntroduccion(LogicaRCP logica, SesionRCP sesion, GestorMenuPausa menu,
                                      PanelResultadosRCP resultados, GameObject centerEye)
    {
        Limpiar("Panel_IntroduccionRCP");

        var ctrl = new GameObject("Panel_IntroduccionRCP");
        var panel = ctrl.AddComponent<PanelIntroduccionRCP>();
        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye.transform);
        SetFloat(anclaje, "distancia", 0.65f);       // mismo anclaje que el panel de resultados (no se ven a la vez)
        SetFloat(anclaje, "alturaRelativa", 0.06f);

        // Coordenadas locales: origen al centro; arriba = +280, abajo = -280.
        var canvas = NuevoCanvas("Canvas", raiz.transform, 720f, 560f);
        canvas.transform.localScale = Vector3.one * 0.00092f;
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "SIMULADOR DE RCP", 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 242f), new Vector2(680f, 44f));
        Texto(canvas.transform, "Subtitulo", "Antes de empezar", 20, FontStyles.Normal, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(0f, 208f), new Vector2(680f, 26f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 188f), 680f, COLOR_ACENTO);

        var cuerpo = Texto(canvas.transform, "TextoCuerpo", "…", 22, FontStyles.Normal, COLOR_TEXTO,
              TextAlignmentOptions.TopLeft, new Vector2(0f, 62f), new Vector2(640f, 240f));
        cuerpo.enableAutoSizing = true; cuerpo.fontSizeMin = 16f; cuerpo.fontSizeMax = 22f;

        Texto(canvas.transform, "EtiquetaCodigo", "TU CÓDIGO", 18, FontStyles.Bold, COLOR_ACENTO,
              TextAlignmentOptions.MidlineLeft, new Vector2(-250f, -66f), new Vector2(140f, 24f));
        var bCodigo = BotonSimple(canvas.transform, "BotonCodigo", "Toca aquí para escribir tu código",
              new Vector2(0f, -116f), new Vector2(640f, 56f), 26);
        var tCodigo = bCodigo.transform.Find("Etiqueta").GetComponent<TMP_Text>();
        tCodigo.enableAutoSizing = true; tCodigo.fontSizeMin = 16f; tCodigo.fontSizeMax = 26f;

        var bComenzar = BotonSimple(canvas.transform, "BotonComenzar", "COMENZAR",
              new Vector2(0f, -210f), new Vector2(360f, 62f), 26);
        var fondoComenzar = bComenzar.GetComponent<Image>();

        var ayuda = Texto(canvas.transform, "TextoAyuda", "Escribe tu código para poder comenzar.", 18, FontStyles.Italic, COLOR_AVISO,
              TextAlignmentOptions.Center, new Vector2(0f, -256f), new Vector2(640f, 26f));
        ayuda.enableAutoSizing = true; ayuda.fontSizeMin = 12f; ayuda.fontSizeMax = 18f;

        Set(panel, "logica", logica);
        Set(panel, "sesion", sesion);
        Set(panel, "resultados", resultados);
        Set(panel, "menuPausa", menu);
        Set(panel, "raiz", raiz);
        Set(panel, "textoCuerpo", cuerpo);
        Set(panel, "textoCodigo", tCodigo);
        Set(panel, "botonCodigo", bCodigo);
        Set(panel, "botonComenzar", bComenzar);
        Set(panel, "fondoBotonComenzar", fondoComenzar);
        Set(panel, "textoAyuda", ayuda);
        raiz.SetActive(false);
        Selection.activeGameObject = ctrl;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers de UI (mismo patrón que ConstruirPanelResultadosRCP)
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
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.transform.SetAsFirstSibling();
        return img;
    }

    static Image Rect(Transform padre, string nombre, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Linea(Transform padre, string nombre, Vector2 pos, float ancho, Color color)
    {
        Rect(padre, nombre, pos, new Vector2(ancho, 2f), color);
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

        var t = Texto(go.transform, "Etiqueta", etiqueta, tamTexto, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(16f, 8f));
        t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = tamTexto;
        return sel;
    }

    // ── Helpers de escena / reflexión ─────────────────────────────────────

    static T Hijo<T>(Transform padre, string nombre) where T : Component
    {
        var t = padre.Find(nombre);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void Limpiar(params string[] nombres)
    {
        var escena = EditorSceneManager.GetActiveScene();
        foreach (var go in escena.GetRootGameObjects())
            foreach (var n in nombres)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ProtocoloRCP] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ProtocoloRCP] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
