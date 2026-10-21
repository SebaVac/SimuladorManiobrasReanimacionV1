#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneSimulador la sesión cronometrada de RCP:
///   - componente <see cref="SesionRCP"/> en el GameObject de LogicaRCP (GestorRCP),
///   - <c>Cronometro_SesionRCP</c>: cuenta regresiva head-locked ("2:00" / "Empieza a comprimir"),
///   - <c>Panel_ResultadosRCP</c>: panel de resultados de desempeño (cinco filas con semáforo y
///     botones "Repetir sesión" / "Volver a calibración").
/// Estética teal diegética (como el Debriefing del módulo clínico). Idempotente: destruye y
/// recrea los dos GameObjects. NO toca SceneDiagnostico (aborta si la escena activa no es
/// SceneSimulador) ni ConstruirArranqueSeccion2.
///
/// Anclajes (SceneSimulador, calculados contra el ícono de pausa d=0.5/h=-0.25/lat=-0.18 y los
/// paneles de métricas/guía, ver docs en el informe de la tarea):
///   - Cronómetro: d=1.0, h=+0.16 — sobre la franja de los paneles de métricas/guía.
///   - Resultados: d=0.8, h=+0.06 — su borde inferior queda por encima del ícono de pausa.
///
/// Menú: Tools/Simulador RCP/Construir sesión cronometrada y resultados
/// </summary>
public static class ConstruirPanelResultadosRCP
{
    static readonly Color COLOR_PANEL   = new Color(0.03f, 0.09f, 0.11f, 0.94f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO   = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);
    static readonly Color COLOR_OSCURO  = new Color(0.03f, 0.09f, 0.11f, 1f);

    static readonly string[] NOMBRES_FILA = { "COMPRESIONES", "PROFUNDIDAD", "FRECUENCIA", "POSTURA Y ZONA", "RECESO TORÁCICO" };

    [MenuItem("Tools/Simulador RCP/Construir sesión cronometrada y resultados")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneSimulador")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneSimulador antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_ResultadosRCP", "Cronometro_SesionRCP");

        var logica = Object.FindFirstObjectByType<LogicaRCP>();
        var maestro = Object.FindFirstObjectByType<BotonMaestro>();
        var menu = Object.FindFirstObjectByType<GestorMenuPausa>();
        var centerEye = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (logica == null || maestro == null || menu == null || centerEye == null)
        {
            Debug.LogError("[ResultadosRCP] Falta LogicaRCP, BotonMaestro, GestorMenuPausa o CenterEyeAnchor en la escena.");
            return;
        }

        // ── SesionRCP en el GameObject de LogicaRCP ──────────────────────
        var sesion = logica.GetComponent<SesionRCP>();
        if (sesion == null) sesion = logica.gameObject.AddComponent<SesionRCP>();
        Set(sesion, "logica", logica);

        // ── Cronómetro ───────────────────────────────────────────────────
        var ctrlCrono = new GameObject("Cronometro_SesionRCP");
        var crono = ctrlCrono.AddComponent<CronometroSesionRCP>();
        var raizCrono = new GameObject("Raiz");
        raizCrono.transform.SetParent(ctrlCrono.transform, false);
        var anclajeCrono = raizCrono.AddComponent<AnclajeCabeza>();
        Set(anclajeCrono, "objetivo", centerEye.transform);
        SetFloat(anclajeCrono, "distancia", 1.0f);
        SetFloat(anclajeCrono, "alturaRelativa", 0.16f);

        var canvasCrono = NuevoCanvas("Canvas", raizCrono.transform, 300f, 110f);
        Fondo(canvasCrono.transform, COLOR_PANEL);
        var tTiempo = Texto(canvasCrono.transform, "TextoTiempo", "2:00", 56, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 18f), new Vector2(280f, 72f));
        var tAyuda = Texto(canvasCrono.transform, "TextoAyuda", "Empieza a comprimir", 22, FontStyles.Italic, COLOR_ACENTO,
              TextAlignmentOptions.Center, new Vector2(0f, -38f), new Vector2(280f, 30f));
        tAyuda.enableAutoSizing = true; tAyuda.fontSizeMin = 14f; tAyuda.fontSizeMax = 22f;

        Set(crono, "sesion", sesion);
        Set(crono, "logica", logica);
        Set(crono, "menuPausa", menu);
        Set(crono, "raiz", raizCrono);
        Set(crono, "textoTiempo", tTiempo);
        Set(crono, "textoAyuda", tAyuda);
        raizCrono.SetActive(false);

        // ── Panel de resultados ──────────────────────────────────────────
        var ctrl = new GameObject("Panel_ResultadosRCP");
        var panel = ctrl.AddComponent<PanelResultadosRCP>();
        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye.transform);
        SetFloat(anclaje, "distancia", 0.65f);   // alcance cómodo con la mano (0,5-0,7 m); era 0,80
        SetFloat(anclaje, "alturaRelativa", 0.06f);

        // Coordenadas locales (origen al centro; arriba=+340, abajo=-340).
        var canvas = NuevoCanvas("Canvas", raiz.transform, 720f, 680f);
        canvas.transform.localScale = Vector3.one * 0.00092f;   // a 0,65 m el ancho queda en ~53° (< 55°); era 0,001
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "RESULTADOS DE LA SESIÓN", 26, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 304f), new Vector2(680f, 40f));
        var duracion = Texto(canvas.transform, "TextoDuracion", "Duración de la sesión: 2:00", 20, FontStyles.Normal, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(0f, 270f), new Vector2(680f, 26f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 250f), 680f, COLOR_ACENTO);

        var valores = new TMP_Text[PanelResultadosRCP.FILAS];
        var indicadores = new Image[PanelResultadosRCP.FILAS];
        var etiquetas = new TMP_Text[PanelResultadosRCP.FILAS];
        for (int i = 0; i < PanelResultadosRCP.FILAS; i++)
        {
            float y = 202f - 94f * i;
            string n = "Fila" + (i + 1);
            var tarjeta = Rect(canvas.transform, n + "_Tarjeta", new Vector2(0f, y), new Vector2(680f, 86f), COLOR_TARJETA);

            var lbl = Texto(canvas.transform, n + "_Nombre", NOMBRES_FILA[i], 18, FontStyles.Bold, COLOR_ACENTO,
                  TextAlignmentOptions.MidlineLeft, new Vector2(-250f, y), new Vector2(160f, 70f));
            lbl.enableAutoSizing = true; lbl.fontSizeMin = 12f; lbl.fontSizeMax = 16f;

            var val = Texto(canvas.transform, n + "_Valor", "—", 20, FontStyles.Normal, COLOR_TEXTO,
                  TextAlignmentOptions.MidlineLeft, new Vector2(12.5f, y), new Vector2(355f, 80f));
            val.enableAutoSizing = true; val.fontSizeMin = 14f; val.fontSizeMax = 20f;
            valores[i] = val;

            var ind = Rect(canvas.transform, n + "_Indicador", new Vector2(265f, y), new Vector2(140f, 40f), new Color(0.45f, 0.52f, 0.55f));
            indicadores[i] = ind;
            var et = Texto(ind.transform, "Etiqueta", "SIN DATOS", 16, FontStyles.Bold, COLOR_OSCURO,
                  TextAlignmentOptions.Center, Vector2.zero, new Vector2(132f, 36f));
            et.enableAutoSizing = true; et.fontSizeMin = 12f; et.fontSizeMax = 16f;
            etiquetas[i] = et;
        }

        var bRepetir = BotonSimple(canvas.transform, "BotonRepetir", "REPETIR SESIÓN",
              new Vector2(-175f, -278f), new Vector2(330f, 58f), 20);
        var bVolver = BotonSimple(canvas.transform, "BotonVolverCalibracion", "VOLVER A CALIBRACIÓN",
              new Vector2(175f, -278f), new Vector2(330f, 58f), 20);

        Set(panel, "sesion", sesion);
        Set(panel, "logica", logica);
        Set(panel, "botonMaestro", maestro);
        Set(panel, "menuPausa", menu);
        Set(panel, "raiz", raiz);
        Set(panel, "textoDuracion", duracion);
        SetArray(panel, "valores", valores);
        SetArray(panel, "indicadores", indicadores);
        SetArray(panel, "etiquetasIndicador", etiquetas);
        Set(panel, "botonRepetir", bRepetir);
        Set(panel, "botonVolver", bVolver);
        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[ResultadosRCP] Sesión cronometrada, cronómetro y panel de resultados construidos y guardados en SceneSimulador.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers de UI (mismo patrón que ConstruirPanel9Debriefing)
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
        if (p == null) { Debug.LogError("[ResultadosRCP] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object comp, string campo, Object[] valores)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ResultadosRCP] Campo no encontrado: " + campo); return; }
        p.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ResultadosRCP] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
