#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 4 — Hallazgos / Examen Físico
/// (GameObject <c>Panel_Hallazgos</c>).
///
/// Estética teal diegética (misma paleta que ConstruirArranqueSeccion2). Head-locked
/// (AnclajeCabeza en Raiz), un poco más alto que los demás paneles para no chocar con
/// el paciente supino que ocupa el centro-bajo del campo de visión. Se activa solo en
/// EstadoCasoClinico.ExamenFisico (lógica en PanelHallazgos).
///
/// Idempotente: borra la versión previa antes de reconstruir.
/// Menú: Tools/Razonamiento Clínico/Construir Panel 4 (Hallazgos)
/// </summary>
public static class ConstruirPanel4Hallazgos
{
    static readonly Color COLOR_PANEL  = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO  = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE  = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 4 (Hallazgos)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_Hallazgos");

        var gestor      = Object.FindObjectOfType<GestorCasoClinico>();
        var interaccion = Object.FindObjectOfType<InteraccionPaciente>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        Transform regiones  = null;
        var pac = GameObject.Find("Paciente_Completo");
        if (pac != null) { var r = pac.transform.Find("Regiones"); if (r != null) regiones = r; }
        if (gestor == null || interaccion == null)
            Debug.LogWarning("[Panel4] No se encontró GestorCasoClinico / InteraccionPaciente.");
        if (regiones == null)
            Debug.LogWarning("[Panel4] No se encontró Paciente_Completo/Regiones — el campo regionesRoot quedará vacío.");

        var ctrl = new GameObject("Panel_Hallazgos");
        var comp = ctrl.AddComponent<PanelHallazgos>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", 0.22f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 660f, 460f);
        Fondo(canvas.transform, COLOR_PANEL);

        Texto(canvas.transform, "Titulo", "EXAMEN FÍSICO", 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, new Vector2(0f, 190f), new Vector2(600f, 44f));
        Linea(canvas.transform, "Regla", new Vector2(0f, 162f), 600f, COLOR_ACENTO);

        var nombre = Texto(canvas.transform, "NombreRegion", string.Empty, 26, FontStyles.Bold,
              COLOR_ACENTO, TextAlignmentOptions.Center, new Vector2(0f, 120f), new Vector2(600f, 40f));

        var hallazgo = Texto(canvas.transform, "TextoHallazgo",
              "Acerca la mano a una región del paciente para examinarla.", 20, FontStyles.Normal,
              COLOR_TEXTO, TextAlignmentOptions.Top, new Vector2(0f, 10f), new Vector2(600f, 180f));

        var continuar = BotonSimple(canvas.transform, "BotonContinuar", "CONTINUAR A ANAMNESIS",
              new Vector2(0f, -188f), new Vector2(460f, 60f));

        Set(comp, "raiz", raiz);
        Set(comp, "textoNombreRegion", nombre);
        Set(comp, "textoHallazgo", hallazgo);
        Set(comp, "botonContinuar", continuar);
        Set(comp, "regionesRoot", regiones);
        Set(comp, "pacienteRoot", pac != null ? pac.transform : null);
        Set(comp, "gestorCaso", gestor);
        Set(comp, "interaccionPaciente", interaccion);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel4] Panel_Hallazgos construido y guardado en SceneDiagnostico.");
    }

    // ── Helpers UI ─────────────────────────────────────────────────────────

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
                                          Vector2 pos, Vector2 size)
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

        Texto(go.transform, "Etiqueta", etiqueta, 22, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(20f, 10f));
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
        if (go == null) Debug.LogWarning("[Panel4] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel4] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel4] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
