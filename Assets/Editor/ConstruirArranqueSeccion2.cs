#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en la escena activa (SceneDiagnostico) el arranque de la Sección 2:
///   - SistemaCasoClinico (GestorCasoClinico + MotorEvaluacion + InteraccionPaciente)
///   - GestorNavegacion (GestorNavegacionMenu)
///   - SelectorCasoAleatorio (pool = [CasoEjemploPlaceholder])
///   - PanelSeleccionSubmodo (3 tarjetas + volver, World Space fijo)
///   - PanelIntroduccion (head-locked, solo motivoConsulta + tutorial condicional + Comenzar)
///
/// Idempotente: borra las versiones previas antes de reconstruir.
/// Menú: Tools/Razonamiento Clínico/Construir arranque Sección 2
///
/// Layout (tamaños/posiciones) = decisiones de Claude, revisables — ver reporte.
/// </summary>
public static class ConstruirArranqueSeccion2
{
    const string RUTA_CASO_EJEMPLO =
        "Assets/Scripts/CasoClinico/FichasEjemplo/CasoEjemploPlaceholder.asset";

    // Paleta (coherente con BotonMenuFeedback)
    static readonly Color COLOR_PANEL   = new Color(0.03f, 0.09f, 0.11f, 0.92f);
    static readonly Color COLOR_TARJETA = new Color(0.047f, 0.180f, 0.204f, 0.90f);
    static readonly Color COLOR_TEXTO   = new Color(0.90f, 0.96f, 0.97f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);

    [MenuItem("Tools/Razonamiento Clínico/Construir arranque Sección 2")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("SistemaCasoClinico", "GestorNavegacion", "SelectorCasoAleatorio",
                "PanelSeleccionSubmodo", "PanelIntroduccion", "BarraNavegacionCaso",
                "GestorSeleccionToque", "GestorSeleccionPinch", "GestorSeleccionPinch (prueba)");

        // ── Cámara / manos del rig ──────────────────────────────────────────
        Transform centerEye = BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        Transform manoIzq  = BuscarPorRuta("OVRCameraRig/TrackingSpace/LeftHandAnchor/OVRHandPrefab(Left)");
        Transform manoDer  = BuscarPorRuta("OVRCameraRig/TrackingSpace/RightHandAnchor/OVRHandPrefab(Right)");

        // ── Árbitro de selección por toque ─────────────────────────────────
        var goSelToque = new GameObject("GestorSeleccionToque");
        var gestorToque = goSelToque.AddComponent<GestorSeleccionToque>();
        Set(gestorToque, "esqueletoIzquierdo", manoIzq != null ? manoIzq.GetComponent("OVRSkeleton") : null);
        Set(gestorToque, "esqueletoDerecho",   manoDer != null ? manoDer.GetComponent("OVRSkeleton") : null);

        // ── Sistema del caso ────────────────────────────────────────────────
        var goSistema = new GameObject("SistemaCasoClinico");
        var motor      = goSistema.AddComponent<MotorEvaluacion>();
        var interaccion = goSistema.AddComponent<InteraccionPaciente>();
        var gestor     = goSistema.AddComponent<GestorCasoClinico>();

        Set(gestor, "interaccionPaciente", interaccion);
        Set(gestor, "motorEvaluacion", motor);
        Set(interaccion, "manoIzquierda", manoIzq);
        Set(interaccion, "manoDerecha", manoDer);
        SetArray(interaccion, "regionesAnatomicas", new Object[0]); // se pueblan en el panel de examen físico (paso futuro)

        // ── Navegación ──────────────────────────────────────────────────────
        var goNav = new GameObject("GestorNavegacion");
        var nav = goNav.AddComponent<GestorNavegacionMenu>();

        // ── Selector de caso ────────────────────────────────────────────────
        var goSelector = new GameObject("SelectorCasoAleatorio");
        var selector = goSelector.AddComponent<SelectorCasoAleatorio>();
        var casoEjemplo = AssetDatabase.LoadAssetAtPath<FichaCaso>(RUTA_CASO_EJEMPLO);
        if (casoEjemplo == null)
            Debug.LogWarning("[Constructor] No se encontró " + RUTA_CASO_EJEMPLO + " — asigna el pool a mano.");
        SetList(selector, "poolDeCasos", casoEjemplo != null ? new Object[] { casoEjemplo } : new Object[0]);
        Set(selector, "gestorCasoClinico", gestor);

        // ── Paneles ─────────────────────────────────────────────────────────
        var submodo = ConstruirPanelSubmodo(selector, nav, gestor);
        var intro   = ConstruirPanelIntroduccion(gestor, centerEye);
        ConstruirBarraNavegacion(gestor, centerEye);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Selection.activeGameObject = submodo;
        Debug.Log("[Constructor] Arranque Sección 2 construido y guardado en SceneDiagnostico.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PANEL SELECCIÓN DE SUBMODO
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject ConstruirPanelSubmodo(SelectorCasoAleatorio selector,
                                            GestorNavegacionMenu nav, GestorCasoClinico gestor)
    {
        var ctrl = new GameObject("PanelSeleccionSubmodo");
        var comp = ctrl.AddComponent<PanelSeleccionSubmodo>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", BuscarPorRuta("OVRCameraRig/TrackingSpace/CenterEyeAnchor"));
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 900f, 920f);

        Fondo(canvas.transform, COLOR_PANEL);

        // Encabezado
        Texto(canvas.transform, "Titulo", "RAZONAMIENTO CLÍNICO", 46, FontStyles.Bold,
              COLOR_TEXTO, TextAlignmentOptions.Center, new Vector2(0f, 400f), new Vector2(840f, 60f));
        Texto(canvas.transform, "Subtitulo", "SELECCIÓN DE SUBMODO · SCENEDIAGNOSTICO", 20,
              FontStyles.Normal, COLOR_TENUE, TextAlignmentOptions.Center,
              new Vector2(0f, 356f), new Vector2(840f, 30f));

        // Tarjetas (separación 210 u ≈ 0.21 m para que no se solapen las zonas de pinch)
        var tTut = Tarjeta(canvas.transform, "TarjetaTutorial", "◆", "TUTORIAL",
            "Introducción guiada a la interacción con el paciente virtual", 210f);
        var tGui = Tarjeta(canvas.transform, "TarjetaGuiado", "◈", "GUIADO",
            "Resolución de casos con asistencia y retroalimentación en tiempo real", 0f);
        var tEva = Tarjeta(canvas.transform, "TarjetaEvaluacion", "◉", "EVALUACIÓN",
            "Resolución sin asistencia · desempeño registrado para el resumen final", -210f);

        // Volver
        var volver = BotonSimple(canvas.transform, "BotonVolver", "↩  VOLVER AL MENÚ PRINCIPAL",
            new Vector2(0f, -338f), new Vector2(560f, 56f), 0.05f);

        // Footer
        Texto(canvas.transform, "Footer1", "PUCV · ESCUELA DE INGENIERÍA INFORMÁTICA", 16,
              FontStyles.Normal, COLOR_TENUE, TextAlignmentOptions.Center,
              new Vector2(0f, -410f), new Vector2(840f, 24f));
        Texto(canvas.transform, "Footer2", "FELIPE GODOY & SEBASTIAN SAAVEDRA", 16,
              FontStyles.Normal, COLOR_TENUE, TextAlignmentOptions.Center,
              new Vector2(0f, -434f), new Vector2(840f, 24f));

        Set(comp, "raiz", raiz);
        Set(comp, "tarjetaTutorial", tTut);
        Set(comp, "tarjetaGuiado", tGui);
        Set(comp, "tarjetaEvaluacion", tEva);
        Set(comp, "botonVolver", volver);
        Set(comp, "selectorCaso", selector);
        Set(comp, "navegacion", nav);
        Set(comp, "gestorCaso", gestor);

        return ctrl;
    }

    static SeleccionableToque Tarjeta(Transform padre, string nombre, string icono,
                                      string titulo, string descripcion, float y)
    {
        var go = NuevoBoton(nombre, padre, new Vector2(0f, y), new Vector2(760f, 118f), COLOR_TARJETA, 0.05f);

        Texto(go.transform, "Icono", icono, 40, FontStyles.Bold, COLOR_ACENTO,
              TextAlignmentOptions.Center, new Vector2(-330f, 0f), new Vector2(90f, 90f));
        Texto(go.transform, "TituloTarjeta", titulo, 30, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Left, new Vector2(40f, 26f), new Vector2(520f, 40f));
        Texto(go.transform, "Descripcion", descripcion, 18, FontStyles.Normal, COLOR_TENUE,
              TextAlignmentOptions.Left, new Vector2(40f, -22f), new Vector2(560f, 44f));
        Texto(go.transform, "Chevron", ">", 40, FontStyles.Bold, COLOR_TENUE,
              TextAlignmentOptions.Center, new Vector2(348f, 0f), new Vector2(48f, 60f));

        return go.GetComponent<SeleccionableToque>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PANEL DE INTRODUCCIÓN (head-locked)
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject ConstruirPanelIntroduccion(GestorCasoClinico gestor, Transform centerEye)
    {
        var ctrl = new GameObject("PanelIntroduccion");
        var comp = ctrl.AddComponent<PanelIntroduccion>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.05f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 780f, 540f);
        Fondo(canvas.transform, COLOR_PANEL);

        var motivo = Texto(canvas.transform, "TextoMotivoConsulta",
            "(motivo de consulta)", 26, FontStyles.Normal, COLOR_TEXTO,
            TextAlignmentOptions.TopLeft, new Vector2(0f, 90f), new Vector2(720f, 300f));

        // Bloque Tutorial (contenedor con explicación de las 3 fases)
        var bloque = new GameObject("BloqueTutorial", typeof(RectTransform));
        bloque.transform.SetParent(canvas.transform, false);
        var rtB = (RectTransform)bloque.transform;
        rtB.anchoredPosition = new Vector2(0f, -110f);
        rtB.sizeDelta = new Vector2(720f, 150f);
        Fondo(bloque.transform, new Color(0.05f, 0.14f, 0.16f, 0.85f));
        Texto(bloque.transform, "TextoTutorial",
            "En este caso vas a: 1) examinar al paciente acercando las manos a cada región, " +
            "2) preguntarle (alternativas o texto libre), y 3) pedir exámenes complementarios. " +
            "Al final registrás tu diagnóstico y conducta. [texto placeholder — se afina después]",
            17, FontStyles.Normal, COLOR_TENUE, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(680f, 130f));

        var atras = BotonSimple(canvas.transform, "BotonAtras", "◀  ATRÁS",
            new Vector2(-190f, -210f), new Vector2(230f, 64f), 0.05f);
        var comenzar = BotonSimple(canvas.transform, "BotonComenzar", "COMENZAR",
            new Vector2(160f, -210f), new Vector2(300f, 64f), 0.05f);

        Set(comp, "raiz", raiz);
        Set(comp, "textoMotivoConsulta", motivo);
        Set(comp, "bloqueTutorial", bloque);
        Set(comp, "botonComenzar", comenzar);
        Set(comp, "botonAtras", atras);
        Set(comp, "gestorCaso", gestor);

        raiz.SetActive(false); // arranca oculto
        return ctrl;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BARRA DE NAVEGACIÓN DEL CASO (Atrás / Salir)  — head-locked, abajo
    // ═══════════════════════════════════════════════════════════════════════

    static GameObject ConstruirBarraNavegacion(GestorCasoClinico gestor, Transform centerEye)
    {
        var ctrl = new GameObject("BarraNavegacionCaso");
        var comp = ctrl.AddComponent<BarraNavegacionCaso>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye);
        SetFloat(anclaje, "distancia", 0.60f);
        SetFloat(anclaje, "alturaRelativa", -0.40f);

        var canvas = NuevoCanvas("Canvas", raiz.transform, 720f, 110f);
        Fondo(canvas.transform, new Color(0.03f, 0.09f, 0.11f, 0.85f));

        var atras = BotonSimple(canvas.transform, "BotonAtras", "◀  ATRÁS",
            new Vector2(-180f, 0f), new Vector2(300f, 78f), 0.05f);
        var salir = BotonSimple(canvas.transform, "BotonSalir", "SALIR DEL CASO",
            new Vector2(180f, 0f), new Vector2(300f, 78f), 0.05f);

        Set(comp, "raiz", raiz);
        Set(comp, "botonAtras", atras);
        Set(comp, "botonSalir", salir);
        Set(comp, "gestorCaso", gestor);

        raiz.SetActive(false);
        return ctrl;
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
        var img = go.GetComponent<Image>();
        img.color = color;
        go.transform.SetAsFirstSibling();
        return img;
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

    /// Botón base: Image + BotonMenuFeedback + SeleccionableToque.
    static GameObject NuevoBoton(string nombre, Transform padre, Vector2 pos, Vector2 size,
                                 Color color, float radio)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;

        var fb  = go.AddComponent<BotonMenuFeedback>();
        var sel = go.AddComponent<SeleccionableToque>();
        Set(sel, "feedbackVisual", fb);
        SetFloat(sel, "profundidadToque", radio);
        return go;
    }

    static SeleccionableToque BotonSimple(Transform padre, string nombre, string etiqueta,
                                          Vector2 pos, Vector2 size, float radio)
    {
        var go = NuevoBoton(nombre, padre, pos, size, COLOR_TARJETA, radio);
        Texto(go.transform, "Etiqueta", etiqueta, 24, FontStyles.Bold, COLOR_TEXTO,
              TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(20f, 10f));
        return go.GetComponent<SeleccionableToque>();
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
        if (go == null) Debug.LogWarning("[Constructor] No se encontró: " + ruta);
        return go != null ? go.transform : null;
    }

    static void Set(Object comp, string campo, Object valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Constructor] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Constructor] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetArray(Object comp, string campo, Object[] valores)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Constructor] Campo no encontrado: " + campo); return; }
        p.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetList(Object comp, string campo, Object[] valores) => SetArray(comp, campo, valores);
}
#endif
