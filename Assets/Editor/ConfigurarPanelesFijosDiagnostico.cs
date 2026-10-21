#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SceneDiagnostico: pasa los paneles del módulo de razonamiento clínico de "seguir la cabeza" (AnclajeCabeza) a
/// posiciones FIJAS de la sala. Crea el root <c>PanelesFijosDiagnostico</c> con 5 slots (<see cref="SlotPanel"/>) y
/// coloca la raíz de cada panel como HIJA de su slot, con la pose local necesaria para que el centro de su Canvas
/// coincida con el origen del slot. Sin código en tiempo de ejecución: el panel aparece o desaparece con SetActive de su
/// raíz (por fase o por la pausa) y siempre en el mismo sitio. Quita el AnclajeCabeza de esas raíces; el ícono de pausa
/// y Canvas_PanelPausa siguen anclados a la cabeza.
///
/// Idempotente: NO mueve un slot que ya existe (así conserva lo que ajustaste a mano); si reconstruyes un panel con su
/// builder (que le vuelve a poner AnclajeCabeza), basta ejecutar esto otra vez. No toca MotorEvaluacion, puntaje,
/// GestorCasoClinico, GestorSeleccionToque, PosePaciente, SuperficiePaciente, EntornoBoxConsulta, la ambientación ni el
/// PuntoInicioJugador. No ejecuta ConstruirArranqueSeccion2 ni los builders de entorno.
///
/// Posiciones iniciales (cabeza en el PuntoInicioJugador, (0,0), mirando +Z). Las calculó una búsqueda sobre la malla real
/// del paciente en sus dos poses. El paciente solo se dibuja en ExamenFisico, y el Slot_Principal nunca coexiste con él, así
/// que el Principal NO guarda margen a la piel (sí >= 0,10 m a camilla, mobiliario y ambientación y >= 0,30 m a paredes).
///   Slot_Principal  frente al jugador (azimut 0), 0,75 m, centro a 1,44 m: paneles 1 (selección), 2 (introducción), 5, 6, 7 y 9.
///                   La altura deja el panel más grande (0,92 m) dentro de -45°/+30° con ojos de 1,5 a 1,7 m.
///   Slot_Barra      (sin cambios) a +60°, 1,89 m; ya no queda sobre el Principal. Su controlador está desactivado.
///   Slot_Notas      (sin cambios) izq., 0,75 m a -60°, centro a 1,20 m (más baja). Más cerca de 0° no cabe sin taparle el
///                   Principal al jugador: el mínimo factible es ~52°.
///   Slot_Monitor    izq., 1,0 m a -42°, centro a 1,64 m (sobre Slot_Notas). Es el azimut mínimo que no le tapa el Principal.
///   Slot_Hallazgos  (sin cambios) costado derecho de la camilla, a mitad de su largo; legible desde ambos extremos.
///
/// Menú: Tools/Razonamiento Clínico/Configurar paneles fijos (SceneDiagnostico)
/// </summary>
public static class ConfigurarPanelesFijosDiagnostico
{
    public const string RAIZ = "PanelesFijosDiagnostico";

    class DefSlot { public string nombre; public Vector3 pos; public float yaw; public string desc; public Color color; public string[] paneles; }

    static List<DefSlot> Definiciones()
    {
        float r = 0.75f, az = 60f * Mathf.Deg2Rad;
        float xp = Mathf.Sin(az) * r, zp = Mathf.Cos(az) * r;                  // 0,65 ; 0,375
        float azm = 42f * Mathf.Deg2Rad; float rm = 1.0f; float xm = Mathf.Sin(azm) * rm, zm = Mathf.Cos(azm) * rm;   // Monitor: 1,0 m a 42°
        float yawHall = Mathf.Atan2(1.0f - 0f, 1.3f - 0.85f) * Mathf.Rad2Deg;  // mira hacia el punto (0, 0,85): legible desde la cabecera y desde el otro extremo
        return new List<DefSlot>
        {
            new DefSlot { nombre = "Slot_Principal", pos = new Vector3(0f, 1.44f, 0.75f), yaw = 0f, color = new Color(0.2f, 0.9f, 1f),
                desc = "Paneles 1 (selección de submodo), 2 (introducción), 5 (anamnesis), 6 (exámenes), 7 (registro de diagnóstico) y 9 (debriefing). Nunca coexisten, ni con el paciente dibujado (solo se ve en examen físico). Frente al jugador, a 0,75 m; centro a 1,44 m (la Y del slot). Arrástralo para moverlo.",
                paneles = new[] { "PanelSeleccionSubmodo", "PanelIntroduccion", "Panel_Anamnesis", "Panel_ExamenesComplementarios", "Panel_RegistroDiagnostico", "Panel_Debriefing" } },
            new DefSlot { nombre = "Slot_Barra", pos = new Vector3(xp, 1.89f, zp), yaw = 60f, color = new Color(0.4f, 0.8f, 0.5f),
                desc = "Barra de navegación del caso (ATRÁS / CONTINUAR), visible de ExamenFisico a RegistroDiagnostico. Va SOBRE Slot_Principal: debajo chocaría con los brazos del paciente acostado. Centro a 1,89 m. Arrástralo para moverlo.",
                paneles = new[] { "BarraNavegacionCaso" } },
            new DefSlot { nombre = "Slot_Notas", pos = new Vector3(-xp, 1.20f, zp), yaw = -60f, color = new Color(1f, 0.8f, 0.3f),
                desc = "Hoja de notas (panel con botones), visible en examen físico, anamnesis, exámenes y registro. Costado izquierdo, a 0,75 m y -60° de la cabeza, más baja que el resto: centro a 1,20 m. Arrástralo para moverlo.",
                paneles = new[] { "Panel_HojaNotas" } },
            new DefSlot { nombre = "Slot_Monitor", pos = new Vector3(-xm, 1.64f, zm), yaw = -42f, color = new Color(1f, 0.5f, 0.5f),
                desc = "Monitor de signos vitales (panel 3, solo lectura), a la izquierda y por encima de Slot_Notas, a 1,0 m y -42° de la cabeza para no taparle el panel principal. Centro a 1,64 m. Arrástralo para moverlo.",
                paneles = new[] { "Panel_SignosVitales" } },
            new DefSlot { nombre = "Slot_Hallazgos", pos = new Vector3(1.0f, 1.35f, 1.3f), yaw = yawHall, color = new Color(0.9f, 0.6f, 1f),
                desc = "Panel 4 (hallazgos del examen físico). Costado derecho de la camilla, a mitad de su largo, mirando hacia el punto medio entre la cabecera y el otro extremo: se lee desde delante y desde detrás de la camilla. Centro a 1,35 m. Arrástralo para moverlo.",
                paneles = new[] { "Panel_Hallazgos" } },
        };
    }

    [MenuItem("Tools/Razonamiento Clínico/Configurar paneles fijos (SceneDiagnostico)")]
    public static void Configurar()
    {
        var escena = EditorSceneManager.GetActiveScene();
        if (escena.name != "SceneDiagnostico")
        {
            EditorUtility.DisplayDialog("Escena incorrecta", "Abre SceneDiagnostico antes de ejecutar esto.\nActual: " + escena.name, "OK");
            return;
        }

        var root = GameObject.Find(RAIZ);
        if (root == null) { root = new GameObject(RAIZ); root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); }

        int movidos = 0, anclajesQuitados = 0; var informe = new List<string>();
        foreach (var def in Definiciones())
        {
            var t = root.transform.Find(def.nombre);
            if (t == null)
            {
                var go = new GameObject(def.nombre); go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(def.pos, Quaternion.Euler(0f, def.yaw, 0f)); go.transform.localScale = Vector3.one;
                t = go.transform;
            }
            var slot = t.GetComponent<SlotPanel>(); if (slot == null) slot = t.gameObject.AddComponent<SlotPanel>();

            Vector2 tam = Vector2.zero;
            foreach (var nombrePanel in def.paneles)
            {
                var raiz = RaizDe(nombrePanel);
                if (raiz == null) { Debug.LogError("[PanelesFijos] No encuentro la raíz del panel " + nombrePanel); continue; }
                var cv = raiz.GetComponentInChildren<Canvas>(true);
                var rt = (RectTransform)cv.transform;
                Vector3 centro = raiz.transform.InverseTransformPoint(rt.TransformPoint(rt.rect.center));     // centro del Canvas en el espacio de la raíz
                var anc = raiz.GetComponent<AnclajeCabeza>();
                if (anc != null) { Object.DestroyImmediate(anc); anclajesQuitados++; }
                raiz.transform.SetParent(t, false);
                raiz.transform.localPosition = -centro; raiz.transform.localRotation = Quaternion.identity; raiz.transform.localScale = Vector3.one;
                tam = new Vector2(Mathf.Max(tam.x, rt.rect.width * rt.lossyScale.x), Mathf.Max(tam.y, rt.rect.height * rt.lossyScale.y));
                movidos++;
                informe.Add(nombrePanel + " -> " + def.nombre + " (offset local " + (-centro).ToString("F3") + ")");
            }
            slot.Configurar(def.desc, tam, def.color);
            EditorUtility.SetDirty(slot);
        }

        // Puente de pausa: confirma que sigue contando a todos los paneles y que no hay slots nulos en su lista de respaldo
        int ocultables = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (mb is IPanelOcultable) ocultables++;
        int nulos = 0;
        var puente = Object.FindFirstObjectByType<PuenteVisibilidadPausa>();
        if (puente != null)
        {
            var lista = new SerializedObject(puente).FindProperty("panelesOcultables");
            for (int i = 0; i < lista.arraySize; i++) if (lista.GetArrayElementAtIndex(i).objectReferenceValue == null) nulos++;
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[PanelesFijos] " + movidos + " paneles en slots, " + anclajesQuitados + " AnclajeCabeza quitados. IPanelOcultable en la escena: " + ocultables
                  + "; slots nulos en la lista de respaldo del puente de pausa: " + nulos + ".\n" + string.Join("\n", informe));
    }

    /// <summary>Raíz visual de un panel: el campo serializado "raiz" del script del controlador (que queda siempre activo).</summary>
    static GameObject RaizDe(string nombreControlador)
    {
        GameObject ctrl = null;
        foreach (var g0 in Resources.FindObjectsOfTypeAll<GameObject>())          // incluye controladores desactivados
            if (g0.name == nombreControlador && g0.scene.IsValid() && g0.scene == EditorSceneManager.GetActiveScene()) { ctrl = g0; break; }
        if (ctrl == null) return null;
        foreach (var mb in ctrl.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            var p = new SerializedObject(mb).FindProperty("raiz");
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is GameObject g) return g;
        }
        return null;
    }
}
#endif
