#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú: Tools > Medir Bounds Reales (Instanciados)
///
/// Mide Renderer.bounds de objetos YA INSTANCIADOS en la escena.
/// Renderer.bounds es world-space y ya incorpora todo el chain de Transform:
///   localScale del GO × localScale de hijos FBX × lossyScale de padres
/// → los números son los tamaños REALES visibles en pantalla, sin suposiciones.
///
/// También reporta lossyScale del raíz del GO para detectar si hay una escala
/// interna del FBX que contradice el localScale seteado en el script.
/// </summary>
public static class MedirBoundsInstanciados
{
    // Nombres a buscar bajo EntornoSalaUrgencias (primer instancia de cada uno)
    static readonly string[] BUSCAR = {
        "wall_1", "wall_door", "wall_window",
        "floor_1",
        "machine_1", "machine_2",
        "tray_1", "tray_2",
        "cupboard_bottom", "cupboard_top"
    };

    [MenuItem("Tools/Medir Bounds Reales (Instanciados)")]
    static void Medir()
    {
        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════════════════════════════");
        sb.AppendLine("  BOUNDS REALES — Renderer.bounds (world-space, con transforms)");
        sb.AppendLine("  lossyScale = escala efectiva en world: incluye cadena FBX interna");
        sb.AppendLine("══════════════════════════════════════════════════════════════\n");

        var sala = GameObject.Find("EntornoSalaUrgencias");
        if (sala == null)
        {
            sb.AppendLine("⚠ NO SE ENCONTRÓ 'EntornoSalaUrgencias' en la escena.");
            sb.AppendLine("  Ejecutá primero: Tools > Montar Sala Urgencias (Hospital Pack)");
            Debug.LogWarning(sb.ToString());
            EditorUtility.DisplayDialog("Error", "'EntornoSalaUrgencias' no está en la escena.", "OK");
            return;
        }

        sb.AppendLine("┌───────────────────────┬──────────┬──────────┬──────────┬──────────────┐");
        sb.AppendLine("│ Modelo                │  X (m)   │  Y (m)   │  Z (m)   │  lossyScale  │");
        sb.AppendLine("├───────────────────────┼──────────┼──────────┼──────────┼──────────────┤");

        float wallY      = -1f;
        float wallLossyY = -1f;

        foreach (string nombre in BUSCAR)
        {
            // Primer child cuyo nombre coincide exactamente
            var target = EncontrarPorNombre(sala.transform, nombre);
            if (target == null)
            {
                sb.AppendLine($"│ {nombre,-23}│   --     │   --     │   --     │ no encontrado│");
                continue;
            }

            // Encapsular todos los Renderers del GO y sus hijos (FBX puede tener sub-meshes)
            var renderers = target.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                sb.AppendLine($"│ {nombre,-23}│   --     │   --     │   --     │ sin Renderer │");
                continue;
            }

            Bounds total = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                total.Encapsulate(renderers[i].bounds);

            Vector3 sz   = total.size;
            float lossy  = target.transform.lossyScale.y;  // Y es la altura → la más relevante

            if (nombre == "wall_1") { wallY = sz.y; wallLossyY = lossy; }

            sb.AppendLine($"│ {nombre,-23}│ {sz.x,8:F3} │ {sz.y,8:F3} │ {sz.z,8:F3} │ {lossy,12:F4} │");
        }
        sb.AppendLine("└───────────────────────┴──────────┴──────────┴──────────┴──────────────┘\n");

        // ── ANÁLISIS DE ESCALA ─────────────────────────────────────────────────
        sb.AppendLine("── ANÁLISIS ──────────────────────────────────────────────────────────────");
        if (wallY > 0f && wallLossyY > 0f)
        {
            // CORRECCIÓN DEL BUG: dividir por la altura BASE a lossyScale=1, no por la altura world actual.
            // wallY    = altura world real con el ENV_SCALE ya aplicado (ej: 4.000 m con ENV_SCALE=2)
            // wallLossyY = lossyScale efectivo (ej: 2.0)
            // wallBaseY  = altura a scale=1 sin ENV_SCALE (ej: 4.000/2.0 = 2.000 m)
            // Factor correcto = objetivo / wallBaseY  (siempre desde scale=1 origen)
            float wallBaseY    = wallY / wallLossyY;
            float factorMin    = 2.4f / wallBaseY;   // para pared mínimo arquitectónico 2.4 m
            float factorOpt    = 3.0f / wallBaseY;   // para pared óptimo 3.0 m

            sb.AppendLine($"  wall_1 altura world (con lossyScale actual): {wallY:F4} m");
            sb.AppendLine($"  wall_1 lossyScale.y actual:                  {wallLossyY:F4}");
            sb.AppendLine($"  wall_1 altura BASE a scale=1:                {wallBaseY:F4} m  (= {wallY:F4} / {wallLossyY:F4})");
            sb.AppendLine($"");
            sb.AppendLine($"  Para pared de 2.4 m → ENV_SCALE desde scale=1: {factorMin:F2}×  (target 2.4 / base {wallBaseY:F3})");
            sb.AppendLine($"  Para pared de 3.0 m → ENV_SCALE desde scale=1: {factorOpt:F2}×  (target 3.0 / base {wallBaseY:F3})");
            sb.AppendLine($"  ⚠ Estos factores van sobre el pack a scale 1 — NO multipliques sobre ENV_SCALE actual.");

            if (wallY < 0.5f)
                sb.AppendLine($"\n  🔴 ALERTA: pared < 50 cm en world space. Probable escala interna FBX no compensada.");
            else if (wallY < 1.8f)
                sb.AppendLine($"\n  🟡 SOSPECHOSO: pared < 1.8 m. Verificar si hay escala interna FBX no compensada.");
            else if (wallY >= 2.0f && wallY <= 5.0f)
                sb.AppendLine($"\n  ✓ Altura en rango arquitectónico plausible (2–5 m).");
            else if (wallY > 5.0f)
                sb.AppendLine($"\n  🟡 Pared > 5m. Considerar reducir ENV_SCALE.");
        }
        else
        {
            sb.AppendLine("  ⚠ wall_1 no encontrado — no se puede calcular factor de corrección.");
        }

        // ── REFERENCIA HUMANA ──────────────────────────────────────────────────
        sb.AppendLine("\n── REFERENCIA HUMANA ──────────────────────────────────────────────────────");

        // Capsule Unity default: height total = 2 unidades (escala 1 → 2m).
        // Para 1.70m: scale Y = 0.85, centro en Y = 0.85
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "REF_Humano_1.70m";
        Undo.RegisterCreatedObjectUndo(capsule, "Ref Humano");

        // Posicionar al lado de la pared trasera (Z=-2 en sala ×2) para comparación visual directa
        capsule.transform.position   = new Vector3(-5f, 0.85f, -2f);
        capsule.transform.localScale = new Vector3(0.4f, 0.85f, 0.4f);

        // Material rojo para que sea inmediatamente visible
        var matRef = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (matRef.shader != null)
        {
            matRef.SetColor("_BaseColor", new Color(1f, 0.1f, 0.1f, 1f));
            capsule.GetComponent<Renderer>().sharedMaterial = matRef;
        }

        float capHeight = capsule.GetComponent<Renderer>().bounds.size.y;
        sb.AppendLine($"  Capsule '{capsule.name}' creada en la escena (roja).");
        sb.AppendLine($"  Altura medida: {capHeight:F3} m (debe ser ≈ 1.700 m).");
        sb.AppendLine($"  Posición: {capsule.transform.position}  ← al lado de la pared trasera");
        sb.AppendLine($"  → En Scene View: compará visualmente la cápsula roja con las paredes.");

        sb.AppendLine("\n══════════════════════════════════════════════════════════════");
        Debug.Log(sb.ToString());

        // Seleccionar y hacer ping en Project window
        Selection.activeGameObject = capsule;

        // Frame the reference object in SceneView
        SceneView.lastActiveSceneView?.Frame(new Bounds(capsule.transform.position, Vector3.one * 3f), false);

        EditorUtility.DisplayDialog(
            "Bounds medidos ✓",
            $"Resultados en la Console.\n\n" +
            $"Cápsula roja 'REF_Humano_1.70m' colocada al lado de la pared trasera.\n" +
            $"Compará visualmente su altura con las paredes en el Scene View.\n\n" +
            $"wall_1 altura medida: {wallY:F4} m",
            "OK — Ver Console y Scene View");
    }

    static Transform EncontrarPorNombre(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(includeInactive: true))
            if (t.gameObject.name == nombre)
                return t;
        return null;
    }
}
#endif
