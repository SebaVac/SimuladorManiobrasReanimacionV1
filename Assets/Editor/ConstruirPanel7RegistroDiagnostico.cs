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
        // Grid de 2 columnas fijas (auditado 2026-09-22): antes cada Etiqueta/Valor
        // tenía un ancho elegido a mano sin relación real con dónde empezaba el
        // campo vecino — los rects de Etiqueta y Valor se superponían de verdad en
        // la fila Sexo/Edad y en Establecimiento (confirmado con GetWorldCorners:
        // LblEstab llegaba a x=-450, 98u más allá del borde izquierdo de la Hoja
        // en x=-352 — parte del label ni siquiera caía sobre el papel — y ValEstab
        // arrancaba en x=-345, DENTRO del rect de LblEstab, de ahí la fusión visual
        // "STABLECIMIENTHospital..."). Columnas medidas con GetPreferredValues()
        // contra el texto real más largo de cada campo (label más largo,
        // "ESTABLECIMIENTO" @14pt bold, mide 143u; entra con margen en 170u):
        //   Etiqueta: [-330, -160] (ancho 170)   Valor: [-140, 330] (ancho 470,
        //   empieza 20u después de la etiqueta, dentro del margen de la Hoja).
        const float ETQ_X = -245f, ETQ_ANCHO = 170f;
        const float VAL_X = 95f,   VAL_ANCHO = 470f;

        Etiqueta(canvas.transform, "LblPaciente", "PACIENTE", new Vector2(ETQ_X, 194f), ETQ_ANCHO);
        var vPaciente = Valor(canvas.transform, "ValPaciente", new Vector2(VAL_X, 194f), VAL_ANCHO, 20f);

        // Sexo | Edad: dos pares etiqueta/valor lado a lado, con su propio hueco
        // (gutter en x=[-10,10]) para que ninguno de los 4 rects se toque.
        Etiqueta(canvas.transform, "LblSexo", "SEXO", new Vector2(-295f, 156f), 70f);
        var vSexo = Valor(canvas.transform, "ValSexo", new Vector2(-130f, 156f), 240f, 18f);
        Etiqueta(canvas.transform, "LblEdad", "EDAD", new Vector2(45f, 156f), 70f);
        var vEdad = Valor(canvas.transform, "ValEdad", new Vector2(210f, 156f), 240f, 18f);

        Etiqueta(canvas.transform, "LblEstab", "ESTABLECIMIENTO", new Vector2(ETQ_X, 118f), ETQ_ANCHO);
        var vEstab = Valor(canvas.transform, "ValEstab", new Vector2(VAL_X, 118f), VAL_ANCHO, 17f);
        // Auto-tamaño (17→12) como salvaguarda: el nombre placeholder actual mide
        // ~402u a 17pt y entra sin problema en los 470u de ancho (con wrap ya
        // activado por Texto() como respaldo), pero un nombre más largo en un
        // caso futuro se reduce en vez de desbordar sobre la fila de abajo.
        vEstab.enableAutoSizing = true;
        vEstab.fontSizeMin = 12f;
        vEstab.fontSizeMax = 17f;

        Etiqueta(canvas.transform, "LblIngreso", "INGRESO", new Vector2(ETQ_X, 80f), ETQ_ANCHO);
        var vIngreso = Valor(canvas.transform, "ValIngreso", new Vector2(VAL_X, 80f), VAL_ANCHO, 18f);

        Linea(canvas.transform, "Regla2", new Vector2(0f, 54f), 660f);

        // ── Diagnóstico ────────────────────────────────────────────────────
        Texto(canvas.transform, "LblDiagnostico", "DIAGNÓSTICO", 20, FontStyles.Bold,
              COLOR_TINTA, TextAlignmentOptions.Left, new Vector2(-330f, 26f), new Vector2(360f, 30f));

        // Campo (borde + relleno + texto), con SeleccionableToque en el borde.
        // Alto 160 (era 132, +28 con el borde superior fijo — hay 74u libres hasta
        // BotonRegistrar, de sobra) + auto-tamaño en el texto (auditado 2026-09-22):
        // con altura fija 96 y overflowMode=Overflow (default de TMP) + sin
        // autoSizing, un diagnóstico largo que wrapea a más de 4 líneas @20pt
        // simplemente se dibujaba por debajo del recuadro sin control (confirmado
        // con GetPreferredValues: un texto realista de 260 caracteres necesita
        // 114.3u de alto contra los 96u disponibles). El wrap horizontal en sí
        // YA estaba bien (enableWordWrapping=true, ancho 600 dentro del relleno de
        // 654u) — el problema era solo vertical.
        var campo = CajaBorde("CampoDiagnostico", canvas.transform, new Vector2(0f, -76f),
                              new Vector2(660f, 160f), COLOR_CAMPO, out Image campoBorde, out Image campoRelleno);
        var campoSel = campo.AddComponent<SeleccionableToque>();
        SetFloat(campoSel, "profundidadToque", 0.05f);
        var txtDiagnostico = Texto(campo.transform, "TextoDiagnostico",
            "Toca para escribir el diagnóstico", 20, FontStyles.Italic, COLOR_TINTA_TENUE,
            TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(600f, 124f));
        // Con la caja más alta, un diagnóstico realista ya entra a 20pt sin
        // achicarse (114.3u < 124u) — enableAutoSizing queda como salvaguarda para
        // un texto aún más largo, mismo criterio que ValEstab.
        txtDiagnostico.enableAutoSizing = true;
        txtDiagnostico.fontSizeMin = 12f;
        txtDiagnostico.fontSizeMax = 20f;

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
