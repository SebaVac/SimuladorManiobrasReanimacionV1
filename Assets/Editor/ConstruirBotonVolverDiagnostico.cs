#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agrega el botón secundario «VOLVER» a los paneles Anamnesis, Exámenes complementarios y Registro de
/// diagnóstico de SceneDiagnostico (2026-11-19). Va debajo de «Continuar» / «Registrar» y lleva a la fase anterior
/// vía <see cref="GestorCasoClinico.VolverAFaseAnterior"/>; Hallazgos no lo tiene. NO usa BarraNavegacionCaso.
///
/// Idempotente: si el panel ya tiene «BotonVolverFase» solo re-cablea el campo y restaura texto/estilo/posición; el
/// canvas se extiende UNA sola vez (al crear el botón). Para no mover nada, el canvas crece hacia abajo: su
/// localPosition baja H/2 y cada hijo sube H/2 en anchoredPosition (queda en la misma posición del mundo), y los
/// fondos (anclados a estirar) simplemente se alargan. El botón se clona de «BotonContinuar» para heredar la
/// misma interacción (SeleccionableToque + BotonMenuFeedback, capa y ventana de gracia) y solo se cambia la
/// apariencia (secundaria: más pequeño, fondo tenue, etiqueta sin negrita).
/// OJO: NO correr «Configurar paneles fijos» después. Ese menú recentra el canvas de cada panel en su slot, y como estos tres
/// canvases crecieron hacia abajo, desplazaría su contenido +0,031 / +0,033 / +0,016 m (Anamnesis / Exámenes / Registro).
/// Los paneles NO se reconstruyeron con sus Construir*: solo se les añadió un hijo y el canvas se extendió.
/// </summary>
public static class ConstruirBotonVolverDiagnostico
{
    private const string NOMBRE_BOTON = "BotonVolverFase";
    private const string TEXTO = "« VOLVER";
    private const float ANCHO = 220f, ALTO = 44f, FUENTE = 18f;
    private const float SEPARACION = 8f;   // entre el borde inferior del último elemento y el botón
    private const float MARGEN_INFERIOR = 20f;

    [MenuItem("Tools/Razonamiento Clínico/Agregar botón Volver (SceneDiagnostico)")]
    public static void Ejecutar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            Debug.LogError("[ConstruirBotonVolver] Abre SceneDiagnostico primero (escena activa: " + escena.name + ").");
            return;
        }

        var gestor = Object.FindObjectOfType<GestorCasoClinico>(true);
        int hechos = 0;
        hechos += Procesar<PanelAnamnesis>("BotonContinuar", "TextoAyudaBloqueado", gestor);
        hechos += Procesar<PanelExamenesComplementarios>("BotonContinuar", "TextoAyudaBloqueado", gestor);
        hechos += Procesar<PanelRegistroDiagnostico>("BotonRegistrar", null, gestor);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[ConstruirBotonVolver] " + hechos + " paneles procesados; escena guardada.");
    }

    private static int Procesar<T>(string nombreBotonPrincipal, string nombreAyuda, GestorCasoClinico gestor) where T : Component
    {
        var panel = Object.FindObjectOfType<T>(true);
        if (panel == null) { Debug.LogError("[ConstruirBotonVolver] No hay " + typeof(T).Name + " en la escena."); return 0; }

        var so = new SerializedObject(panel);
        var raiz = so.FindProperty("raiz").objectReferenceValue as GameObject;
        var canvas = raiz != null ? raiz.GetComponentInChildren<Canvas>(true) : null;
        if (canvas == null) { Debug.LogError("[ConstruirBotonVolver] " + typeof(T).Name + ": sin raíz/canvas."); return 0; }
        var crt = (RectTransform)canvas.transform;

        var principal = crt.Find(nombreBotonPrincipal) as RectTransform;
        if (principal == null) { Debug.LogError("[ConstruirBotonVolver] " + typeof(T).Name + ": no encuentro " + nombreBotonPrincipal + "."); return 0; }

        var existente = crt.Find(NOMBRE_BOTON) as RectTransform;
        RectTransform boton = existente;
        if (boton == null)
        {
            // Borde inferior del último elemento (botón principal o texto de ayuda).
            float fondoUltimo = principal.anchoredPosition.y - principal.sizeDelta.y * 0.5f;
            if (!string.IsNullOrEmpty(nombreAyuda))
            {
                var ayuda = crt.Find(nombreAyuda) as RectTransform;
                if (ayuda != null) fondoUltimo = Mathf.Min(fondoUltimo, ayuda.anchoredPosition.y - ayuda.sizeDelta.y * 0.5f);
            }
            float yBoton = fondoUltimo - SEPARACION - ALTO * 0.5f;
            float bordeNecesario = yBoton - ALTO * 0.5f - MARGEN_INFERIOR;   // borde inferior que debe tener el canvas (negativo)
            float bordeActual = -crt.sizeDelta.y * 0.5f;
            float H = Mathf.Max(0f, bordeActual - bordeNecesario);           // cuánto crece hacia abajo

            // 1) Crecer sin mover nada: hijos +H/2, canvas baja H/2 (en unidades del mundo).
            foreach (Transform ch in crt)
            {
                var r = ch as RectTransform;
                if (r == null) continue;
                // Solo los hijos anclados al centro llevan posición absoluta; los que estiran (Fondo, Hoja) se alargan solos.
                if (r.anchorMin == r.anchorMax) r.anchoredPosition += new Vector2(0f, H * 0.5f);
            }
            crt.sizeDelta = new Vector2(crt.sizeDelta.x, crt.sizeDelta.y + H);
            crt.localPosition += new Vector3(0f, -H * 0.5f * crt.localScale.y, 0f);

            // 2) Clonar el botón principal.
            var clon = Object.Instantiate(principal.gameObject, crt);
            clon.name = NOMBRE_BOTON;
            boton = (RectTransform)clon.transform;
            boton.anchoredPosition = new Vector2(0f, yBoton + H * 0.5f);   // yBoton estaba en coords previas; los hijos subieron H/2
        }

        // Apariencia secundaria + texto (también al re-ejecutar).
        boton.sizeDelta = new Vector2(ANCHO, ALTO);
        var img = boton.GetComponent<Image>();
        var fb = boton.GetComponent<BotonMenuFeedback>();
        var col = new Color(0.10f, 0.15f, 0.17f, 0.55f);
        if (img != null) img.color = col;
        if (fb != null)
        {
            fb.colorNormal = col;
            fb.colorHover = new Color(0.16f, 0.28f, 0.32f, 0.85f);
            fb.colorPresionado = new Color(0.04f, 0.09f, 0.10f, 0.90f);
        }
        var txt = boton.GetComponentInChildren<TMP_Text>(true);
        if (txt != null)
        {
            txt.text = TEXTO;
            txt.fontSize = FUENTE;
            txt.fontStyle = FontStyles.Normal;
            txt.color = new Color(0.78f, 0.86f, 0.88f, 1f);
            txt.enableAutoSizing = false;
        }
        boton.gameObject.SetActive(true);

        // Cableado del campo serializado del panel.
        so.Update();
        var prop = so.FindProperty("botonVolver");
        if (prop == null) { Debug.LogError("[ConstruirBotonVolver] " + typeof(T).Name + " no tiene el campo botonVolver."); return 0; }
        prop.objectReferenceValue = boton.GetComponent<SeleccionableToque>();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel);
        Debug.Log("[ConstruirBotonVolver] " + typeof(T).Name + ": botón " + (existente != null ? "actualizado" : "creado") +
                  ", canvas " + crt.sizeDelta + ".");
        return 1;
    }
}
#endif
