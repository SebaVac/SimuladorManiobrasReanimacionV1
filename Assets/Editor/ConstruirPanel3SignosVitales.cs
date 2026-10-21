#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye en SceneDiagnostico el Panel 3 — Monitor de signos vitales (GameObject
/// <c>Panel_SignosVitales</c>). Estilo monitor de cabecera: pantalla oscura, borde y cifras en
/// acento teal, todos los valores del mismo color. Solo lectura (sin SeleccionableToque).
///
/// Posición (head-locked, AnclajeCabeza): d=0.60, h=+0.17, lateral=-0.54. Es la columna libre a la
/// IZQUIERDA de los paneles de fase (que ocupan |x| &lt;= 0.38 en el plano d=0.60), por encima del
/// ícono de pausa (y &lt;= -0.47) y de la franja donde el paciente se ve desde el visor (pitch
/// &lt;= -6.6°, es decir y &lt;= -0.07 en ese plano): el monitor ocupa y de -0.05 a +0.39.
///
/// Idempotente. Menú: Tools/Razonamiento Clínico/Construir Panel 3 (Signos vitales)
/// </summary>
public static class ConstruirPanel3SignosVitales
{
    static readonly Color COLOR_PANEL   = new Color(0.01f, 0.04f, 0.05f, 0.96f);
    static readonly Color COLOR_ACENTO  = new Color(0.35f, 0.80f, 0.85f, 1f);
    static readonly Color COLOR_TENUE   = new Color(0.62f, 0.74f, 0.78f, 1f);
    static readonly Color COLOR_LINEA   = new Color(0.35f, 0.80f, 0.85f, 0.30f);

    const float DISTANCIA = 0.60f, ALTURA = 0.17f, LATERAL = -0.54f;
    const float ANCHO = 250f, ALTO = 440f;

    static readonly string[] ETIQUETAS = { "FREC. CARDÍACA", "FREC. RESPIRATORIA", "PRESIÓN ARTERIAL", "SATURACIÓN O2", "TEMPERATURA" };
    static readonly string[] UNIDADES  = { "lpm", "rpm", "mmHg", "%", "°C" };
    static readonly string[] EJEMPLOS  = { "105", "22", "150/95", "94", "36,6" };

    [MenuItem("Tools/Razonamiento Clínico/Construir Panel 3 (Signos vitales)")]
    public static void Construir()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta",
                "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        Limpiar("Panel_SignosVitales");

        var gestor = Object.FindFirstObjectByType<GestorCasoClinico>();
        var centerEye = GameObject.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (gestor == null || centerEye == null)
        {
            Debug.LogError("[Panel3] Falta GestorCasoClinico o CenterEyeAnchor en la escena.");
            return;
        }

        var ctrl = new GameObject("Panel_SignosVitales");
        var comp = ctrl.AddComponent<PanelSignosVitales>();

        var raiz = new GameObject("Raiz");
        raiz.transform.SetParent(ctrl.transform, false);
        var anclaje = raiz.AddComponent<AnclajeCabeza>();
        Set(anclaje, "objetivo", centerEye.transform);
        SetFloat(anclaje, "distancia", DISTANCIA);
        SetFloat(anclaje, "alturaRelativa", ALTURA);
        SetFloat(anclaje, "desplazamientoLateral", LATERAL);

        var canvas = NuevoCanvas("Canvas", raiz.transform, ANCHO, ALTO);
        Marco(canvas.transform, "Borde", Vector2.zero, new Vector2(ANCHO, ALTO), COLOR_ACENTO);
        Marco(canvas.transform, "Pantalla", Vector2.zero, new Vector2(ANCHO - 6f, ALTO - 6f), COLOR_PANEL);

        var titulo = Texto(canvas.transform, "Titulo", "SIGNOS VITALES", 20, FontStyles.Bold, COLOR_ACENTO,
              TextAlignmentOptions.Center, new Vector2(0f, 196f), new Vector2(230f, 30f));
        titulo.enableAutoSizing = true; titulo.fontSizeMin = 14f; titulo.fontSizeMax = 20f;
        Marco(canvas.transform, "Regla", new Vector2(0f, 176f), new Vector2(220f, 2f), COLOR_ACENTO);

        var valores = new TMP_Text[PanelSignosVitales.FILAS];
        for (int i = 0; i < PanelSignosVitales.FILAS; i++)
        {
            float yc = 130f - 76f * i;   // centros 130, 54, -22, -98, -174
            string n = "Fila" + (i + 1);

            var et = Texto(canvas.transform, n + "_Etiqueta", ETIQUETAS[i], 14, FontStyles.Bold, COLOR_TENUE,
                  TextAlignmentOptions.MidlineLeft, new Vector2(-34f, yc + 27f), new Vector2(156f, 22f));
            et.enableAutoSizing = true; et.fontSizeMin = 10f; et.fontSizeMax = 13f; et.enableWordWrapping = false;
            var un = Texto(canvas.transform, n + "_Unidad", UNIDADES[i], 16, FontStyles.Bold, COLOR_TENUE,
                  TextAlignmentOptions.MidlineRight, new Vector2(80f, yc + 27f), new Vector2(70f, 22f));
            un.enableAutoSizing = true; un.fontSizeMin = 11f; un.fontSizeMax = 14f; un.enableWordWrapping = false; un.margin = new Vector4(0f, 0f, 4f, 0f); // el glifo final sangra ~1 mm fuera de la caja

            var v = Texto(canvas.transform, n + "_Valor", EJEMPLOS[i], 44, FontStyles.Bold, COLOR_ACENTO,
                  TextAlignmentOptions.Center, new Vector2(0f, yc - 11f), new Vector2(220f, 50f));
            v.enableAutoSizing = true; v.fontSizeMin = 24f; v.fontSizeMax = 44f; v.enableWordWrapping = false;
            valores[i] = v;

            if (i < PanelSignosVitales.FILAS - 1)
                Marco(canvas.transform, n + "_Linea", new Vector2(0f, yc - 38f), new Vector2(210f, 1f), COLOR_LINEA);
        }

        Set(comp, "raiz", raiz);
        Set(comp, "gestorCaso", gestor);
        var so = new SerializedObject(comp);
        var p = so.FindProperty("valores");
        p.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = ctrl;
        Debug.Log("[Panel3] Panel_SignosVitales construido y guardado en SceneDiagnostico.");
    }

    // ── Helpers (mismo patrón que ConstruirPanel9Debriefing) ──────────────

    static Canvas NuevoCanvas(string nombre, Transform padre, float w, float h)
    {
        var go = new GameObject(nombre, typeof(Canvas));
        go.transform.SetParent(padre, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        go.transform.localScale = Vector3.one * 0.001f;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        return c;
    }

    static Image Marco(Transform padre, string nombre, Vector2 pos, Vector2 size, Color color)
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
        if (p == null) { Debug.LogError("[Panel3] Campo no encontrado: " + campo + " en " + comp.GetType().Name); return; }
        p.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object comp, string campo, float valor)
    {
        var so = new SerializedObject(comp);
        var p = so.FindProperty(campo);
        if (p == null) { Debug.LogError("[Panel3] Campo no encontrado: " + campo); return; }
        p.floatValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
