#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 7 — Registro de Diagnóstico
/// (GameObject <c>Panel_RegistroDiagnostico</c>).
///
/// Estética DELIBERADAMENTE distinta al resto del módulo: ficha clínica de papel
/// (crema/beige, tinta oscura, bordes finos), no el teal diegético. Intencional.
///
/// Head-locked igual que PanelIntroduccion (AnclajeCabeza en Raiz). Se activa solo
/// en EstadoCasoClinico.RegistroDiagnostico (lógica en PanelRegistroDiagnostico).
///
/// Idempotente: borra la versión previa antes de reconstruir.
/// Menú: Tools/Razonamiento Clínico/Construir Panel 7 (Registro de Diagnóstico)
/// </summary>
public static class ConstruirPanel7RegistroDiagnostico
{
    // Paleta ficha de papel
    static readonly Color COLOR_HOJA        = new Color(0.960f, 0.930f, 0.855f, 1f);
    static readonly Color COLOR_BORDE_HOJA  = new Color(0.300f, 0.260f, 0.200f, 1f);
    static readonly Color COLOR_TINTA       = new Color(0.160f, 0.130f, 0.100f, 1f);
    static readonly Color COLOR_TINTA_TENUE = new Color(0.420f, 0.360f, 0.300f, 1f);
    static readonly Color COLOR_CAMPO       = new Color(0.990f, 0.970f, 0.900f, 1f);
    static readonly Color COLOR_BOTON       = new Color(0.930f, 0.895f, 0.795f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 7 (Registro de Diagnóstico)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_RegistroDiagnostico");

        var gestor      = Object.FindObjectOfType<GestorCasoClinico>();
        var interaccion = Object.FindObjectOfType<InteraccionPaciente>();
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null || interaccion == null)
            Debug.LogWarning("[Panel7] No se encontró GestorCasoClinico / InteraccionPaciente en la escena.");

        // ── Jerarquía base ─────────────────────────────────────────────────
        var ctrl = new GameObject("Panel_RegistroDiagnostico");
        var comp = ctrl.AddComponent<PanelRegistroDiagnostico>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 720f, 620f);

        // Borde exterior + hoja
        Rect(canvas.transform, "BordeHoja", COLOR_BORDE_HOJA, 0f);
        Rect(canvas.transform, "Hoja", COLOR_HOJA, 8f);

        // ── Encabezado ─────────────────────────────────────────────────────
        Texto(canvas.transform, "Titulo", "FICHA DE REGISTRO CLÍNICO", 34, FontStyles.Bold,
              COLOR_TINTA, TextAlignmentOptions.Center, new Vector2(0f, 258f), new Vector2(660f, 46f));
        Linea(canvas.transform, "Regla1", new Vector2(0f, 230f), 660f);

        // ── Datos de solo lectura ──────────────────────────────────────────
        Etiqueta(canvas.transform, "LblPaciente", "PACIENTE", new Vector2(-330f, 194f), 200f);
        var vPaciente = Valor(canvas.transform, "ValPaciente", new Vector2(-120f, 194f), 450f, 20f);

        Etiqueta(canvas.transform, "LblSexo", "SEXO", new Vector2(-330f, 156f), 110f);
        var vSexo = Valor(canvas.transform, "ValSexo", new Vector2(-238f, 156f), 170f, 18f);
        Etiqueta(canvas.transform, "LblEdad", "EDAD", new Vector2(-30f, 156f), 90f);
        var vEdad = Valor(canvas.transform, "ValEdad", new Vector2(48f, 156f), 260f, 18f);

        Etiqueta(canvas.transform, "LblEstab", "ESTABLECIMIENTO", new Vector2(-330f, 118f), 240f);
        var vEstab = Valor(canvas.transform, "ValEstab", new Vector2(-120f, 118f), 450f, 17f);

        Etiqueta(canvas.transform, "LblIngreso", "INGRESO", new Vector2(-330f, 80f), 200f);
        var vIngreso = Valor(canvas.transform, "ValIngreso", new Vector2(-120f, 80f), 450f, 18f);

        Linea(canvas.transform, "Regla2", new Vector2(0f, 54f), 660f);

        // ── Diagnóstico ────────────────────────────────────────────────────
        Texto(canvas.transform, "LblDiagnostico", "DIAGNÓSTICO", 20, FontStyles.Bold,
              COLOR_TINTA, TextAlignmentOptions.Left, new Vector2(-330f, 26f), new Vector2(360f, 30f));

        // Campo (borde + relleno + texto), con SeleccionableToque en el borde.
        var campo = CajaBorde("CampoDiagnostico", canvas.transform, new Vector2(0f, -62f),
                              new Vector2(660f, 132f), COLOR_CAMPO, out Image campoBorde, out Image campoRelleno);
        var campoSel = campo.AddComponent<SeleccionableToque>();
        SetFloat(campoSel, "profundidadToque", 0.05f);
        var txtDiagnostico = Texto(campo.transform, "TextoDiagnostico",
            "Toca para escribir el diagnóstico", 20, FontStyles.Italic, COLOR_TINTA_TENUE,
            TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(600f, 96f));

        // Botón registrar (borde + relleno + etiqueta), SeleccionableToque en el borde.
        var boton = CajaBorde("BotonRegistrar", canvas.transform, new Vector2(0f, -236f),
                              new Vector2(420f, 68f), COLOR_BOTON, out Image botonBorde, out Image _);
        var botonSel = boton.AddComponent<SeleccionableToque>();
        SetFloat(botonSel, "profundidadToque", 0.05f);
        Texto(boton.transform, "Etiqueta", "REGISTRAR DIAGNÓSTICO", 20, FontStyles.Bold,
              COLOR_TINTA, TextAlignmentOptions.Center, Vector2.zero, new Vector2(380f, 48f));

        // ── Cableado del componente ────────────────────────────────────────
        Set(comp, "raiz", raiz);
        Set(comp, "textoPaciente", vPaciente);
        Set(comp, "textoSexo", vSexo);
        Set(comp, "textoEdad", vEdad);
        Set(comp, "textoEstablecimiento", vEstab);
        Set(comp, "textoFechaHora", vIngreso);
        Set(comp, "campoDiagnostico", campoSel);
        Set(comp, "textoDiagnostico", txtDiagnostico);
        Set(comp, "fondoCampoDiagnostico", campoRelleno);
        Set(comp, "bordeCampoDiagnostico", campoBorde);
        Set(comp, "botonRegistrar", botonSel);
        Set(comp, "bordeBotonRegistrar", botonBorde);
        Set(comp, "gestorCaso", gestor);
        Set(comp, "interaccionPaciente", interaccion);

        raiz.SetActive(false); // arranca oculto; PanelRegistroDiagnostico lo muestra en RegistroDiagnostico

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel7] Panel_RegistroDiagnostico construido y guardado en SceneDiagnostico.");
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

    /// Rectángulo que ocupa todo el padre, opcionalmente con un margen (inset) en px.
    static Image Rect(Transform padre, string nombre, Color color, float inset)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        go.transform.SetAsLastSibling();
        return img;
    }

    static void Linea(Transform padre, string nombre, Vector2 pos, float ancho)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(ancho, 2f);
        var img = go.GetComponent<Image>();
        img.color = COLOR_BORDE_HOJA;
        img.raycastTarget = false;
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

    static void Etiqueta(Transform padre, string nombre, string txt, Vector2 pos, float ancho)
    {
        Texto(padre, nombre, txt, 14, FontStyles.Bold, COLOR_TINTA_TENUE,
              TextAlignmentOptions.Left, pos, new Vector2(ancho, 24f));
    }

    static TextMeshProUGUI Valor(Transform padre, string nombre, Vector2 pos, float ancho, float size)
    {
        return Texto(padre, nombre, "—", size, FontStyles.Normal, COLOR_TINTA,
                     TextAlignmentOptions.Left, pos, new Vector2(ancho, 30f));
    }

    /// Caja con borde: GO exterior = Image de borde (+ SeleccionableToque lo agrega el
    /// llamador), hijo "Relleno" con inset de 3 px.
    static GameObject CajaBorde(string nombre, Transform padre, Vector2 pos, Vector2 size,
                                Color relleno, out Image borde, out Image imgRelleno)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        borde = go.GetComponent<Image>();
        borde.color = COLOR_BORDE_HOJA;
        borde.raycastTarget = false;

        imgRelleno = Rect(go.transform, "Relleno", relleno, 3f);
        return go;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS DE REFLEXIÓN / SERIALIZED
    // ═══════════════════════════════════════════════════════════════════════

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
        if (go == null) Debug.LogWarning("[Panel7] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel7] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel7] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
