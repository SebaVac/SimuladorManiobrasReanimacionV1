#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramientas del catálogo ampliado de exámenes (modo Evaluación). Tres pasos, todos
/// idempotentes (se pueden re-ejecutar sin duplicar nada):
///   1. Crear/actualizar <c>CatalogoExamenes.asset</c> (19 entradas: las 18 pedidas + "Exploración
///      clínica del dolor", que SCA ya traía y no tiene equivalente en el catálogo de 18).
///   2. Migrar los 7 casos: renombrar sus exámenes a los nombres del catálogo, partir los
///      agrupados (EPA "Laboratorio", SARS "Pruebas de sangre") y agregar los resultados
///      anormales PROVISIONALES como exámenes Indiferentes.
///   3. Generar docs/matriz_examenes.md (+ .csv) con la matriz 7 casos × catálogo, para que un
///      profesional corrija la clasificación.
/// Menú: Tools/Razonamiento Clínico/Catálogo de exámenes/...
/// </summary>
public static class ConfigurarCatalogoExamenes
{
    const string RUTA_CATALOGO = "Assets/Scripts/CasoClinico/CatalogoExamenes.asset";
    const string CARPETA_CASOS = "Assets/Scripts/CasoClinico/FichasEjemplo/";

    // Orden del pool (y de las columnas de la matriz).
    static readonly string[] CASOS = { "CasoSCA", "CasoTBC", "CasoEPA", "CasoEPOC", "CasoNeumonia", "CasoSARS", "CasoBronquitisAguda" };

    // ── Catálogo: nombre, categoría, resultado normal (cifras típicas) ───────────────────────────
    static readonly string[][] CATALOGO =
    {
        new[] { "Radiografía de tórax", "Imágenes", "Sin infiltrados ni consolidación; silueta cardiomediastínica normal y senos costofrénicos libres." },
        new[] { "Tomografía de tórax", "Imágenes", "Parénquima pulmonar sin lesiones; mediastino y pleura normales." },
        new[] { "Electrocardiograma", "Cardiología", "Ritmo sinusal, FC 75 lpm, eje normal, sin alteraciones del ST-T." },
        new[] { "Ecocardiograma", "Cardiología", "Función sistólica conservada (FEVI aprox. 60%), sin alteraciones valvulares ni derrame pericárdico." },
        new[] { "Troponina", "Cardiología", "Dentro de rango normal (< 0,04 ng/mL)." },
        new[] { "BNP/NT-proBNP", "Cardiología", "Normal (BNP < 100 pg/mL; NT-proBNP < 300 pg/mL)." },
        new[] { "Hemograma", "Laboratorio general", "Hb 14 g/dL, leucocitos 7.000 por mm³ con fórmula normal, plaquetas 250.000 por mm³." },
        new[] { "Proteína C reactiva", "Laboratorio general", "Normal (< 5 mg/L)." },
        new[] { "Electrolitos plasmáticos", "Laboratorio general", "Na 140 mEq/L, K 4,2 mEq/L, Cl 102 mEq/L; dentro de rango." },
        new[] { "Función renal", "Laboratorio general", "Creatinina 0,9 mg/dL, BUN 14 mg/dL; normal." },
        new[] { "Glicemia", "Laboratorio general", "Normal (aprox. 90 mg/dL en ayunas)." },
        new[] { "Dímero D", "Laboratorio general", "Negativo (< 500 ng/mL)." },
        new[] { "Gases arteriales", "Gases", "pH 7,40, PaO2 95 mmHg, PaCO2 40 mmHg, HCO3 24 mEq/L; normales." },
        new[] { "Baciloscopia", "Microbiología", "Negativa, sin bacilos ácido-alcohol resistentes." },
        new[] { "Cultivo de expectoración", "Microbiología", "Flora normal de vía aérea superior, sin patógeno aislado." },
        new[] { "PCR viral / panel respiratorio", "Microbiología", "Negativo para los agentes virales del panel." },
        new[] { "PPD", "Microbiología", "Induración < 5 mm, negativa." },
        new[] { "Espirometría", "Función pulmonar", "Normal: VEF1/CVF > 0,70 y VEF1 > 80% del predicho." },
        // 19ª entrada: SCA ya traía este examen (y un error frecuente 'omitio:' lo referencia); no existe
        // equivalente en las 18 pedidas, así que se agrega como entrada propia en vez de perderlo.
        new[] { "Exploración clínica del dolor", "Evaluación clínica", "Sin dolor referido en reposo (EVA 0/10)." },
    };

    // ── Migración de los casos ──────────────────────────────────────────────────────────────────
    // Nombre viejo → nombre del catálogo (mismo examen, mismo contenido).
    static readonly Dictionary<string, string> RENOMBRES = new Dictionary<string, string>
    {
        { "Marcadores cardíacos (troponina)",        "Troponina" },
        { "Baciloscopia de esputo",                  "Baciloscopia" },
        { "Cultivo de esputo",                       "Cultivo de expectoración" },
        { "PPD (derivado proteico purificado)",      "PPD" },
        { "Cultivo de secreción",                    "Cultivo de expectoración" },
        { "Secreción respiratoria (PCR viral)",      "PCR viral / panel respiratorio" },
    };

    // Examen agrupado → varios del catálogo, con el contenido repartido (sin perder nada).
    static readonly Dictionary<string, string[][]> DIVISIONES = new Dictionary<string, string[][]>
    {
        // EPA "Laboratorio": 'BNP elevado (>500 pg/mL), troponina levemente elevada.'
        { "Laboratorio", new[] {
            new[] { "BNP/NT-proBNP", "Elevado (>500 pg/mL)." },
            new[] { "Troponina",     "Levemente elevada." } } },
        // SARS "Pruebas de sangre": 'Leucopenia con linfopenia, PCR elevada.'
        { "Pruebas de sangre", new[] {
            new[] { "Hemograma",            "Leucopenia con linfopenia." },
            new[] { "Proteína C reactiva",  "Elevada." } } },
    };

    // Overrides PROVISIONALES: resultado anormal coherente con el caso, clasificados Indiferentes.
    // caso → { nombre, resultado }
    static readonly Dictionary<string, string[][]> INDIFERENTES = new Dictionary<string, string[][]>
    {
        { "CasoNeumonia", new[] {
            new[] { "Proteína C reactiva", "Elevada (aprox. 120 mg/L)." },
            new[] { "Gases arteriales",    "Hipoxemia leve (PaO2 aprox. 62 mmHg), pH normal." },
            new[] { "Exploración clínica del dolor", "Dolor pleurítico al inspirar (EVA 4/10)." } } },
        { "CasoEPA", new[] {
            new[] { "Gases arteriales", "Hipoxemia (PaO2 aprox. 55 mmHg) con hipocapnia leve." },
            new[] { "Función renal",    "Creatinina levemente elevada (1,4 mg/dL)." },
            new[] { "Exploración clínica del dolor", "Sin dolor referido en reposo (EVA 0/10)." } } },
        { "CasoSCA", new[] {
            new[] { "Glicemia", "Elevada (aprox. 210 mg/dL)." } } },
        { "CasoTBC", new[] {
            new[] { "Hemograma", "Anemia leve (Hb aprox. 11 g/dL), sin leucocitosis." },
            new[] { "Exploración clínica del dolor", "Sin dolor referido en reposo (EVA 0/10)." } } },
        { "CasoSARS", new[] {
            new[] { "Tomografía de tórax", "Opacidades en vidrio esmerilado bilaterales." },
            new[] { "Exploración clínica del dolor", "Sin dolor referido en reposo (EVA 0/10)." } } },
        { "CasoEPOC", new[] {
            new[] { "Exploración clínica del dolor", "Sin dolor referido en reposo (EVA 0/10)." } } },
        { "CasoBronquitisAguda", new[] {
            new[] { "Exploración clínica del dolor", "Sin dolor referido en reposo (EVA 0/10)." } } },
    };

    // ═══════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/Razonamiento Clínico/Catálogo de exámenes/1. Crear o actualizar catálogo")]
    public static void CrearCatalogo()
    {
        var cat = AssetDatabase.LoadAssetAtPath<CatalogoExamenes>(RUTA_CATALOGO);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<CatalogoExamenes>();
            AssetDatabase.CreateAsset(cat, RUTA_CATALOGO);
        }
        cat.entradas = new List<EntradaCatalogoExamen>();
        foreach (var f in CATALOGO)
            cat.entradas.Add(new EntradaCatalogoExamen { nombreExamen = f[0], categoria = f[1], resultadoNormal = f[2] });
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log("[CatalogoExamenes] " + cat.entradas.Count + " entradas en " + RUTA_CATALOGO);
    }

    [MenuItem("Tools/Razonamiento Clínico/Catálogo de exámenes/2. Migrar los 7 casos al catálogo")]
    public static void MigrarCasos()
    {
        foreach (var nombreCaso in CASOS)
        {
            var caso = AssetDatabase.LoadAssetAtPath<FichaCaso>(CARPETA_CASOS + nombreCaso + ".asset");
            if (caso == null) { Debug.LogError("[CatalogoExamenes] No se encontró " + nombreCaso); continue; }

            var nueva = new List<ExamenComplementario>();
            foreach (var e in caso.examenesDisponibles)
            {
                if (e == null) continue;
                if (DIVISIONES.TryGetValue(e.nombreExamen, out var partes))
                {
                    foreach (var p in partes)
                        Agregar(nueva, p[0], p[1], e.clasificacion);
                    continue;
                }
                string nombre = RENOMBRES.TryGetValue(e.nombreExamen, out var nuevoNombre) ? nuevoNombre : e.nombreExamen;
                Agregar(nueva, nombre, e.resultado, e.clasificacion);
            }

            if (INDIFERENTES.TryGetValue(nombreCaso, out var indiferentes))
                foreach (var p in indiferentes)
                    Agregar(nueva, p[0], p[1], ClasificacionExamen.Indiferente);

            caso.examenesDisponibles = nueva;
            EditorUtility.SetDirty(caso);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[CatalogoExamenes] 7 casos migrados al catálogo.");
    }

    // Idempotente: si ya hay un examen con ese nombre, no lo duplica.
    static void Agregar(List<ExamenComplementario> lista, string nombre, string resultado, ClasificacionExamen clasificacion)
    {
        foreach (var x in lista) if (x.nombreExamen == nombre) return;
        lista.Add(new ExamenComplementario { nombreExamen = nombre, resultado = resultado, clasificacion = clasificacion });
    }

    [MenuItem("Tools/Razonamiento Clínico/Catálogo de exámenes/3. Generar matriz de exámenes (docs/)")]
    public static void GenerarMatriz()
    {
        var cat = AssetDatabase.LoadAssetAtPath<CatalogoExamenes>(RUTA_CATALOGO);
        if (cat == null) { Debug.LogError("[CatalogoExamenes] Falta el catálogo."); return; }

        var casos = new List<FichaCaso>();
        foreach (var n in CASOS) casos.Add(AssetDatabase.LoadAssetAtPath<FichaCaso>(CARPETA_CASOS + n + ".asset"));

        string Letra(ClasificacionExamen c) => c == ClasificacionExamen.Pertinente ? "P" : c == ClasificacionExamen.Indiferente ? "I" : "N";

        var md = new StringBuilder();
        md.AppendLine("# Matriz de exámenes — 7 casos × catálogo");
        md.AppendLine();
        md.AppendLine("Generada por `Tools/Razonamiento Clínico/Catálogo de exámenes/3. Generar matriz` (borrador pendiente de validación clínica).");
        md.AppendLine();
        md.AppendLine("- **P** = pertinente: pedirlo suma y entra en el denominador del componente Exámenes (20 puntos).");
        md.AppendLine("- **I** = indiferente: ni suma ni resta (no cuenta en el denominador ni en la penalización). Son los resultados anormales PROVISIONALES.");
        md.AppendLine("- **N** = no pertinente: pedirlo resta (0,25 × el valor de un examen pertinente). Es el valor por defecto de todo lo que la ficha no trae.");
        md.AppendLine();
        md.AppendLine("## Clasificación");
        md.AppendLine();
        md.Append("| Categoría | Examen |");
        foreach (var c in casos) md.Append(" ").Append(c.idCaso).Append(" |");
        md.AppendLine();
        md.Append("|---|---|");
        foreach (var c in casos) md.Append(":-:|");
        md.AppendLine();
        foreach (var e in cat.entradas)
        {
            md.Append("| ").Append(e.categoria).Append(" | ").Append(e.nombreExamen).Append(" |");
            foreach (var c in casos) md.Append(" ").Append(Letra(cat.Clasificar(c, e.nombreExamen))).Append(" |");
            md.AppendLine();
        }
        md.AppendLine();
        md.AppendLine("## Resultados que trae cada ficha (P e I)");
        md.AppendLine();
        md.AppendLine("Todo examen que no aparece aquí devuelve el resultado normal del catálogo (sección siguiente).");
        md.AppendLine();
        foreach (var c in casos)
        {
            md.AppendLine("### " + c.idCaso + " — " + c.nombreCaso);
            md.AppendLine();
            md.AppendLine("| Examen | Clasif. | Resultado |");
            md.AppendLine("|---|:-:|---|");
            foreach (var e in c.examenesDisponibles)
                md.AppendLine("| " + e.nombreExamen + " | " + Letra(e.clasificacion) + " | " + e.resultado + " |");
            md.AppendLine();
        }
        md.AppendLine("## Resultado normal del catálogo");
        md.AppendLine();
        md.AppendLine("| Categoría | Examen | Resultado normal |");
        md.AppendLine("|---|---|---|");
        foreach (var e in cat.entradas)
            md.AppendLine("| " + e.categoria + " | " + e.nombreExamen + " | " + e.resultadoNormal + " |");

        var csv = new StringBuilder();
        csv.AppendLine("caso;examen;categoria;clasificacion;origen;resultado");
        foreach (var c in casos)
            foreach (var e in cat.entradas)
            {
                bool enFicha = false;
                string res = e.resultadoNormal;
                foreach (var x in c.examenesDisponibles) if (x.nombreExamen == e.nombreExamen) { enFicha = true; res = x.resultado; }
                csv.AppendLine(string.Join(";", c.idCaso, e.nombreExamen, e.categoria, Letra(cat.Clasificar(c, e.nombreExamen)),
                    enFicha ? "ficha" : "catalogo", "\"" + res.Replace("\"", "\"\"") + "\""));
            }

        string carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "matriz_examenes.md"), md.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(carpeta, "matriz_examenes.csv"), csv.ToString(), new UTF8Encoding(true));
        Debug.Log("[CatalogoExamenes] Matriz escrita en " + carpeta);
    }
}
#endif
