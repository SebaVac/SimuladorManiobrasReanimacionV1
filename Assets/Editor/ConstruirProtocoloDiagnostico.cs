#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Protocolo de estudio v1.1.0, Parte B, en SceneDiagnostico:
///   - <c>Panel_IntroduccionDiagnostico</c> (<see cref="PanelIntroduccionDiagnostico"/>): su raíz visual queda como HIJA de
///     Slot_Principal, igual que los demás paneles fijos (centro del canvas = origen del slot).
///   - <c>Registro_CasoClinico</c> (<see cref="RegistroCasoClinico"/>): escribe resultados_diagnostico.csv.
///   - Amplía el Debriefing YA existente (no lo reconstruye): línea «Resultados guardados» y referencias nuevas.
///   - Conecta PanelSeleccionSubmodo con la introducción (permanece oculto mientras esté pendiente).
/// Idempotente: destruye y recrea solo lo que añade. NO toca SceneInicio ni SceneSimulador (aborta si la escena activa no es
/// SceneDiagnostico), no reconstruye ningún otro panel y no ejecuta ConstruirArranqueSeccion2 ni ConstruirBoxConsultaDiagnostico.
/// Si luego ejecutas Tools/Razonamiento Clínico/Configurar paneles fijos, el panel nuevo está en la lista de Slot_Principal
/// y conserva su sitio; el puente de pausa lo descubre solo (IPanelOcultable).
///
/// Menú: Tools/Razonamiento Clínico/Construir introducción y registro del estudio (SceneDiagnostico)
/// </summary>
public static class ConstruirProtocoloDiagnostico
{
    static readonly Color COLOR_PANEL   = new Color(0.03f, 0.09f, 0.11f, 0.94f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO   = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);
    static readonly Color COLOR_AVISO   = new Color(0.95f, 0.85f, 0.15f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir introducción y registro del estudio (SceneDiagnostico)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        var gestor = Object.FindFirstObjectByType<GestorCasoClinico>(FindObjectsInactive.Include);
        var inter = Object.FindFirstObjectByType<InteraccionPaciente>(FindObjectsInactive.Include);
        var menu = Object.FindFirstObjectByType<GestorMenuPausa>(FindObjectsInactive.Include);
        var pose = Object.FindFirstObjectByType<PosePaciente>(FindObjectsInactive.Include);
        var entrada = Object.FindFirstObjectByType<EntradaControlesTouch>(FindObjectsInactive.Include);
        var selector = Object.FindFirstObjectByType<SelectorCasoAleatorio>(FindObjectsInactive.Include);
        var panel2 = Object.FindFirstObjectByType<PanelSeleccionSubmodo>(FindObjectsInactive.Include);
        var deb = Object.FindFirstObjectByType<PanelDebriefing>(FindObjectsInactive.Include);
        var slotGo = GameObject.Find("PanelesFijosDiagnostico/Slot_Principal");
        if (gestor == null || inter == null || menu == null || selector == null || panel2 == null || deb == null || slotGo == null)
        {
            Debug.LogError("[ProtocoloDiagnostico] Falta GestorCasoClinico, InteraccionPaciente, GestorMenuPausa, SelectorCasoAleatorio, PanelSeleccionSubmodo, PanelDebriefing o Slot_Principal.");
            return;
        }

        var registro = ConstruirRegistro(gestor, inter, menu, pose, entrada);
        var intro = ConstruirIntroduccion(gestor, selector, menu, slotGo.transform);
        ExtenderDebriefing(deb, registro);
        Set(panel2, "introduccion", intro);

        EditorUtility.SetDirty(panel2); EditorUtility.SetDirty(deb);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[ProtocoloDiagnostico] Introducción, registro del estudio y Debriefing ampliado construidos y guardados en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    static RegistroCasoClinico ConstruirRegistro(GestorCasoClinico gestor, InteraccionPaciente inter, GestorMenuPausa menu,
                                                 PosePaciente pose, EntradaControlesTouch entrada)
    {
        Limpiar("Registro_CasoClinico");
        var go = new GameObject("Registro_CasoClinico");
        var reg = go.AddComponent<RegistroCasoClinico>();
        Set(reg, "gestorCaso", gestor);
        Set(reg, "interaccionPaciente", inter);
        Set(reg, "menuPausa", menu);
        Set(reg, "posePaciente", pose);
        Set(reg, "entradaDispositivo", entrada);
        return reg;
    }

    // ═══════════════════════════════════════════════════════════════════════
    static PanelIntroduccionDiagnostico ConstruirIntroduccion(GestorCasoClinico gestor, SelectorCasoAleatorio selector,
                                                              GestorMenuPausa menu, Transform slot)
    {
        // Si ya existía (reconstrucción), se destruye también su raíz, que vive bajo el slot.
        foreach (var viejo in Object.FindObjectsByType<PanelIntroduccionDiagnostico>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var p = new SerializedObject(viejo).FindProperty("raiz");
            if (p != null && p.objectReferenceValue is GameObject r) Object.DestroyImmediate(r);
        }
        Limpiar("Panel_IntroduccionDiagnostico");

        var ctrl = new GameObject("Panel_IntroduccionDiagnostico");
        var panel = ctrl.AddComponent<PanelIntroduccionDiagnostico>();
        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);

        // Canvas centrado en el origen de la raíz; 780 x 600 (0,78 x 0,60 m a escala 0,001): menor que los paneles del slot (hasta 0,90 x 0,92 m).
        var canvas = NuevoCanvas("Canvas", raiz.transform, 780f, 600f);
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "RAZONAMIENTO CLÍNICO", 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 262f), new Vector2(740f, 44f));
        Texto(canvas.transform, "Subtitulo", "Antes de empezar", 20, FontStyles.Normal, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(0f, 228f), new Vector2(740f, 26f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 206f), 740f, COLOR_ACENTO);

        var cuerpo = Texto(canvas.transform, "TextoCuerpo", "…", 22, FontStyles.Normal, COLOR_TEXTO,
              TextAlignmentOptions.TopLeft, new Vector2(0f, 66f), new Vector2(700f, 250f));
        cuerpo.enableAutoSizing = true; cuerpo.fontSizeMin = 16f; cuerpo.fontSizeMax = 22f;

        Texto(canvas.transform, "EtiquetaCodigo", "TU CÓDIGO", 18, FontStyles.Bold, COLOR_ACENTO,
              TextAlignmentOptions.MidlineLeft, new Vector2(-280f, -90f), new Vector2(140f, 24f));
        var bCodigo = BotonSimple(canvas.transform, "BotonCodigo", "Toca aquí para escribir tu código",
              new Vector2(0f, -140f), new Vector2(700f, 58f), 26);
        var tCodigo = bCodigo.transform.Find("Etiqueta").GetComponent<TMP_Text>();
        tCodigo.enableAutoSizing = true; tCodigo.fontSizeMin = 16f; tCodigo.fontSizeMax = 26f;

        var bComenzar = BotonSimple(canvas.transform, "BotonComenzar", "COMENZAR",
              new Vector2(0f, -232f), new Vector2(380f, 64f), 26);

        var ayuda = Texto(canvas.transform, "TextoAyuda", "Escribe tu código para poder comenzar.", 18, FontStyles.Italic, COLOR_AVISO,
              TextAlignmentOptions.Center, new Vector2(0f, -282f), new Vector2(700f, 26f));
        ayuda.enableAutoSizing = true; ayuda.fontSizeMin = 12f; ayuda.fontSizeMax = 18f;

        Set(panel, "raiz", raiz);
        Set(panel, "gestorCaso", gestor);
        Set(panel, "selectorCaso", selector);
        Set(panel, "menuPausa", menu);
        Set(panel, "textoCuerpo", cuerpo);
        Set(panel, "textoCodigo", tCodigo);
        Set(panel, "botonCodigo", bCodigo);
        Set(panel, "botonComenzar", bComenzar);
        Set(panel, "fondoBotonComenzar", bComenzar.GetComponent<Image>());
        Set(panel, "textoAyuda", ayuda);

        // Raíz hija de Slot_Principal (igual que los demás): el centro del canvas coincide con el origen del slot.
        raiz.transform.SetParent(slot, false);
        raiz.transform.localPosition = Vector3.zero; raiz.transform.localRotation = Quaternion.identity; raiz.transform.localScale = Vector3.one;
        raiz.SetActive(false);
        return panel;
    }

    // ═══════════════════════════════════════════════════════════════════════
    static void ExtenderDebriefing(PanelDebriefing deb, RegistroCasoClinico registro)
    {
        var pr = new SerializedObject(deb).FindProperty("raiz");
        var raiz = pr != null ? pr.objectReferenceValue as GameObject : null;
        var canvas = raiz != null ? raiz.GetComponentInChildren<Canvas>(true) : null;
        if (canvas == null) { Debug.LogError("[ProtocoloDiagnostico] No encuentro el canvas del Debriefing."); return; }

        var viejo = canvas.transform.Find("TextoGuardado");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

        // Bajo la línea del dispositivo (y = -374, alto 24): el canvas termina en y = -410.
        var guardado = Texto(canvas.transform, "TextoGuardado", "Resultados guardados", 14, FontStyles.Italic, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(0f, -397f), new Vector2(560f, 20f));
        guardado.enableAutoSizing = true; guardado.fontSizeMin = 10f; guardado.fontSizeMax = 14f;
        guardado.gameObject.SetActive(false);

        Set(deb, "registro", registro);
        Set(deb, "textoGuardado", guardado);
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
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
        go.transform.SetAsFirstSibling();
        return img;
    }

    static Image Rect(Transform padre, string nombre, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
        return img;
    }

    static void Linea(Transform padre, string nombre, Vector2 pos, float ancho, Color color) { Rect(padre, nombre, pos, new Vector2(ancho, 2f), color); }

    static TextMeshProUGUI Texto(Transform padre, string nombre, string txt, float size, FontStyles estilo, Color color,
                                 TextAlignmentOptions align, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = txt; t.fontSize = size; t.fontStyle = estilo; t.color = color; t.alignment = align;
        t.enableWordWrapping = true; t.raycastTarget = false;
        return t;
    }

    static SeleccionableToque BotonSimple(Transform padre, string nombre, string etiqueta, Vector2 pos, Vector2 size, float tamTexto)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = COLOR_TARJETA;
        var fb = go.AddComponent<BotonMenuFeedback>();
        var sel = go.AddComponent<SeleccionableToque>();
        Set(sel, "feedbackVisual", fb);
        SetFloat(sel, "profundidadToque", 0.05f);
        var t = Texto(go.transform, "Etiqueta", etiqueta, tamTexto, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(16f, 8f));
        t.enableAutoSizing = true; t.fontSizeMin = 14f; t.fontSizeMax = tamTexto;
        return sel;
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
        if (p == null) { Debug.LogError("[ProtocoloDiagnostico] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[ProtocoloDiagnostico] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
