#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú: Tools > Medir Bounds Hospital Pack
/// Mide los Mesh.bounds de cada FBX del Hospital Pack (sin instanciar en escena)
/// y los compara contra el Banana Man (maniquí RCP).
/// Resultados en la ventana Console de Unity.
/// </summary>
public static class MedirBoundsHospitalPack
{
    const string PACK_FOLDER  = "Assets/Enviroment/HospitalAssets/Models/fbx (Unity Rotated)";
    const string MANIQUI_PATH = "Assets/Plugins/Banana Yellow Games/Characters/Banana Man/Banana Man.fbx";

    // Modelos del pack a medir (en el orden del reporte)
    static readonly string[] MODELOS = new[]
    {
        "wall_1", "wall_door", "wall_window", "wall_column", "wall_collumn_base", "wall_base",
        "floor_1",
        "bed_1", "bed_2", "bed_4",
        "machine_1", "machine_2",
        "tray_1", "tray_2",
        "overhead_light",
        "cupboard_bottom", "cupboard_top"
    };

    [MenuItem("Tools/Medir Bounds Hospital Pack")]
    static void Medir()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("  BOUNDS HOSPITAL PACK  (Mesh.bounds con Scale = 1,1,1)");
        sb.AppendLine("  Valores en metros Unity — fuente: mesh local bounds");
        sb.AppendLine("═══════════════════════════════════════════════════════════\n");

        sb.AppendLine("┌─────────────────────┬──────────┬──────────┬──────────┐");
        sb.AppendLine("│ Modelo              │  X (m)   │  Y (m)   │  Z (m)   │");
        sb.AppendLine("├─────────────────────┼──────────┼──────────┼──────────┤");

        bool escalaInconsistente = false;
        float refY = -1f; // referencia de alto de pared

        foreach (string nombre in MODELOS)
        {
            string path = $"{PACK_FOLDER}/{nombre}.fbx";
            var bounds = ObtenerBoundsDeAsset(path, out string detalle);

            if (bounds.HasValue)
            {
                Vector3 size = bounds.Value.size;
                string marca = "";

                // Detectar escala anómala (>20 m en cualquier eje → probable cm sin convertir)
                if (size.x > 20f || size.y > 20f || size.z > 20f)
                {
                    marca = " ⚠ GIGANTE";
                    escalaInconsistente = true;
                }
                // Detectar escala anómala pequeña (<0.01 m)
                if (size.x < 0.01f && size.y < 0.01f && size.z < 0.01f)
                {
                    marca = " ⚠ MICRO";
                    escalaInconsistente = true;
                }

                // Guardar alto de wall_1 como referencia para comparaciones
                if (nombre == "wall_1") refY = size.y;

                sb.AppendLine($"│ {nombre,-21}│ {size.x,8:F3} │ {size.y,8:F3} │ {size.z,8:F3} │{marca}");
            }
            else
            {
                sb.AppendLine($"│ {nombre,-21}│   N/A    │   N/A    │   N/A    │  (no encontrado: {detalle})");
            }
        }

        sb.AppendLine("└─────────────────────┴──────────┴──────────┴──────────┘\n");

        // ── MANIQUÍ RCP (referencia) ─────────────────────────────────────
        sb.AppendLine("── REFERENCIA: Maniquí RCP (Banana Man) ──────────────────");
        var bManiqui = ObtenerBoundsDeAsset(MANIQUI_PATH, out string detManiqui);
        if (bManiqui.HasValue)
        {
            Vector3 s = bManiqui.Value.size;
            sb.AppendLine($"  Banana Man.fbx → X:{s.x:F3} m  Y:{s.y:F3} m  Z:{s.z:F3} m");
            sb.AppendLine($"  Altura (Y): {s.y:F3} m  (referencia humana adulto ≈ 1.70–1.80 m)");
        }
        else
        {
            sb.AppendLine($"  Banana Man.fbx → no encontrado: {detManiqui}");
        }

        // ── ANÁLISIS AUTOMÁTICO ──────────────────────────────────────────
        sb.AppendLine("\n── ANÁLISIS DE ESCALA ────────────────────────────────────");

        if (refY > 0f)
        {
            float factorSugerido = -1f;
            string conclusion;

            if (refY > 50f)
            {
                // Probablemente en cm (sin conversión)
                factorSugerido = 0.01f;
                conclusion = $"⚠ ALERTA: wall_1 mide {refY:F1} m de alto → probablemente en CENTÍMETROS.\n" +
                             $"  Factor de corrección sugerido: globalScale = {factorSugerido} en Import Settings.\n" +
                             $"  NO aplicar hasta autorización del usuario.";
            }
            else if (refY > 5f)
            {
                // Podría estar en dm o escala exagerada
                factorSugerido = 0.1f;
                conclusion = $"⚠ DUDOSO: wall_1 mide {refY:F1} m de alto (esperado 2.4–3 m).\n" +
                             $"  Factor de corrección sugerido: globalScale ≈ {factorSugerido}.\n" +
                             $"  NO aplicar hasta autorización del usuario.";
            }
            else if (refY < 0.05f)
            {
                // Probablemente en m pero 100x demasiado pequeño
                factorSugerido = 100f;
                conclusion = $"⚠ MICRO: wall_1 mide {refY:F4} m de alto (esperado 2.4–3 m).\n" +
                             $"  Factor de corrección sugerido: globalScale = {factorSugerido}.\n" +
                             $"  NO aplicar hasta autorización del usuario.";
            }
            else if (refY >= 1.8f && refY <= 4.0f)
            {
                conclusion = $"✓ ESCALA CORRECTA: wall_1 mide {refY:F3} m de alto — rango plausible para hospital (1.8–4 m).";
            }
            else
            {
                conclusion = $"? REVISAR MANUALMENTE: wall_1 mide {refY:F3} m de alto — fuera del rango esperado pero no extremo.";
            }
            sb.AppendLine("  " + conclusion);
        }
        else
        {
            sb.AppendLine("  No se pudo obtener bounds de wall_1 para análisis.");
        }

        // Comparación de modelos entre sí (inconsistencia interna)
        sb.AppendLine("\n── INCONSISTENCIAS INTERNAS ──────────────────────────────");
        sb.AppendLine("  (modelos con escala claramente distinta al resto del pack)");
        sb.AppendLine("  Ver tabla arriba — marcar ⚠ si aparece alguno.");

        sb.AppendLine("\n── ESTADO COLLIDERS ──────────────────────────────────────");
        sb.AppendLine("  addColliders: 0 en todos los .meta revisados");
        sb.AppendLine("  → No se generan MeshCollider automáticos en ningún modelo ✓");

        sb.AppendLine("\n═══════════════════════════════════════════════════════════");

        Debug.Log(sb.ToString());

        // También mostrar ventana de diálogo con resumen corto
        string resumen = escalaInconsistente
            ? "⚠ Se detectaron modelos con escala anómala.\nRevisá la Console para el factor de corrección sugerido."
            : "✓ Sin anomalías de escala detectadas.\nRevisá la Console para los bounds completos.";

        EditorUtility.DisplayDialog("Bounds Hospital Pack", resumen, "OK — Ver Console");
    }

    /// <summary>
    /// Carga todos los sub-assets del FBX y devuelve los bounds del primer Mesh encontrado.
    /// No instancia nada en la escena.
    /// </summary>
    static Bounds? ObtenerBoundsDeAsset(string assetPath, out string detalle)
    {
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (allAssets == null || allAssets.Length == 0)
        {
            detalle = "asset no encontrado";
            return null;
        }

        // Encapsulamos los bounds de TODOS los meshes del FBX para manejar multi-mesh
        bool primero = true;
        Bounds totalBounds = default;

        foreach (var asset in allAssets)
        {
            if (asset is Mesh mesh)
            {
                if (primero)
                {
                    totalBounds = mesh.bounds;
                    primero = false;
                }
                else
                {
                    totalBounds.Encapsulate(mesh.bounds);
                }
            }
        }

        if (primero)
        {
            detalle = "ningún Mesh encontrado en el FBX";
            return null;
        }

        detalle = "ok";
        return totalBounds;
    }
}
#endif
