#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera el paquete de validación clínica (SOLO LECTURA de los assets: no modifica ninguno):
///   docs/validacion_clinica_flags.md / .csv   — todo lo que un profesional debe revisar, por caso
///   docs/errores_sin_condicion.md / .csv      — errores frecuentes sin condición (no se detectan solos)
///   docs/README_validacion.md                 — índice e instrucciones para quien revisa
/// Idempotente: sin fecha ni datos volátiles, regenerar con los mismos assets produce archivos idénticos.
/// Los exámenes no se duplican (docs/matriz_examenes.md) ni los conjuntos de Manejo (docs/conjuntos_manejo.md).
/// </summary>
public static class GenerarValidacionClinica
{
    const string CARPETA = "Assets/Scripts/CasoClinico/FichasEjemplo/";
    static readonly string[] CASOS = { "CasoSCA", "CasoTBC", "CasoEPA", "CasoEPOC", "CasoNeumonia", "CasoSARS", "CasoBronquitisAguda" };

    const string COL_VALID = "Validación (OK / Cambiar)";
    const string COL_COMENT = "Comentario";

    [MenuItem("Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)")]
    public static void Generar()
    {
        var casos = new List<FichaCaso>();
        foreach (var n in CASOS)
        {
            var c = AssetDatabase.LoadAssetAtPath<FichaCaso>(CARPETA + n + ".asset");
            if (c == null) { Debug.LogError("[ValidacionClinica] No existe " + n); return; }
            casos.Add(c);
        }

        string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs"));
        Directory.CreateDirectory(docs);
        var utf8 = new UTF8Encoding(false);
        var utf8Bom = new UTF8Encoding(true);   // CSV con BOM: Excel respeta las tildes

        var md = new StringBuilder();
        var csv = new StringBuilder();
        var mdErr = new StringBuilder();
        var csvErr = new StringBuilder();

        EscribirCabeceraFlags(md);
        csv.AppendLine(Csv("Caso", "Sección", "Id / referencia", "Elemento", "Detalle", "Conjuntos / condición", "Marca revisable", COL_VALID, COL_COMENT));

        mdErr.AppendLine("# Errores frecuentes sin condición (no se detectan automáticamente)");
        mdErr.AppendLine();
        mdErr.AppendLine("Generado por `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` (borrador pendiente de validación clínica).");
        mdErr.AppendLine();
        mdErr.AppendLine("Un error frecuente solo resta puntaje (−5 cada uno, tope −15) si su `idReferenciaCondicion` tiene el formato `prefijo:valor` con prefijo `omitio`, `pregunto`, `examino` o `solicito`. Los de esta lista tienen la condición **vacía** (o con un formato que el motor no reconoce): hoy se muestran en el asset pero **nunca se detectan ni restan**. Decidir para cada uno: ¿qué conducta del jugador debería dispararlo?");
        mdErr.AppendLine();
        csvErr.AppendLine(Csv("Caso", "Descripción", "Condición actual", "Motivo", "Retroalimentación", "Condición propuesta (omitio: / pregunto: / examino: / solicito:)", COL_VALID, COL_COMENT));

        var resumen = new List<string>();
        int totalSinCond = 0;
        foreach (var c in casos)
        {
            int claves = 0, relevantes = 0, sinCond = 0, formatoRaro = 0, manejo = 0;
            foreach (var p in c.preguntasClave) if (p != null && p.esPreguntaClave) claves++;
            foreach (var h in c.hallazgosPorRegion) if (h != null && h.esHallazgoRelevante) relevantes++;
            foreach (var a in c.accionesCriticas) if (a != null && a.tipo == TipoAccion.Manejo) manejo++;
            foreach (var e in c.erroresFrecuentes) { if (e == null) continue; var m = Motivo(e); if (m == 1) sinCond++; else if (m == 2) formatoRaro++; }
            totalSinCond += sinCond + formatoRaro;
            resumen.Add("| " + c.idCaso + " | " + c.nombreCaso + " | " + c.preguntasClave.Count + " (clave: " + claves + ") | " + c.hallazgosPorRegion.Count + " (relevantes: " + relevantes
                        + ") | " + c.accionesCriticas.Count + " (Manejo: " + manejo + ") | " + c.erroresFrecuentes.Count + " (sin condición: " + (sinCond + formatoRaro) + ") | " + c.conjuntosPalabrasClaveDiagnostico.Count + " |");

            // ── MD por caso ──
            md.AppendLine("## " + c.idCaso + " — " + c.nombreCaso);
            md.AppendLine();
            md.AppendLine("Motivo de consulta: " + Celda(c.motivoConsulta));
            md.AppendLine();
            md.AppendLine("Conteos: " + c.preguntasClave.Count + " preguntas (" + claves + " clave), " + c.hallazgosPorRegion.Count + " regiones (" + relevantes + " relevantes), "
                          + c.accionesCriticas.Count + " acciones críticas, " + c.erroresFrecuentes.Count + " errores frecuentes (" + (sinCond + formatoRaro) + " sin condición válida), "
                          + c.conjuntosPalabrasClaveDiagnostico.Count + " conjuntos de diagnóstico, " + c.examenesDisponibles.Count + " exámenes propios (ver `docs/matriz_examenes.md`).");
            md.AppendLine();

            // 1. Preguntas
            md.AppendLine("### " + c.idCaso + " · 1. Preguntas de anamnesis");
            md.AppendLine();
            md.AppendLine("| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | " + COL_VALID + " | " + COL_COMENT + " |");
            md.AppendLine("|---|---|---|:-:|---|---|---|");
            foreach (var p in c.preguntasClave)
            {
                string conj = Conjuntos(p.conjuntosPalabrasClave);
                string legado = Legado(p);
                string todo = legado.Length > 0 ? (conj.Length > 0 ? conj + " · " : "") + legado : conj;
                md.AppendLine("| `" + p.idPregunta + "` | " + Celda(p.preguntaSugerida) + " | " + Celda(p.respuestaPaciente) + " | " + (p.esPreguntaClave ? "Sí" : "No") + " | " + Celda(todo) + " |  |  |");
                csv.AppendLine(Csv(c.idCaso, "1. Preguntas", p.idPregunta, p.preguntaSugerida, p.respuestaPaciente, todo, "esPreguntaClave=" + (p.esPreguntaClave ? "Sí" : "No"), "", ""));
            }
            md.AppendLine();

            // 2. Hallazgos
            md.AppendLine("### " + c.idCaso + " · 2. Hallazgos por región (examen físico)");
            md.AppendLine();
            md.AppendLine("| Región | Hallazgo | esHallazgoRelevante (inferida) | " + COL_VALID + " | " + COL_COMENT + " |");
            md.AppendLine("|---|---|:-:|---|---|");
            foreach (var h in c.hallazgosPorRegion)
            {
                md.AppendLine("| " + Celda(h.regionAnatomica) + " | " + Celda(h.hallazgoTexto) + " | " + (h.esHallazgoRelevante ? "Sí" : "No") + " |  |  |");
                csv.AppendLine(Csv(c.idCaso, "2. Hallazgos", h.regionAnatomica, h.regionAnatomica, h.hallazgoTexto, "", "esHallazgoRelevante=" + (h.esHallazgoRelevante ? "Sí" : "No"), "", ""));
            }
            md.AppendLine();

            // 3. Acciones críticas
            md.AppendLine("### " + c.idCaso + " · 3. Acciones críticas");
            md.AppendLine();
            md.AppendLine("| Descripción | Tipo | Peso | idReferencia | Conjuntos | " + COL_VALID + " | " + COL_COMENT + " |");
            md.AppendLine("|---|---|:-:|---|---|---|---|");
            foreach (var a in c.accionesCriticas)
            {
                string conj = a.tipo == TipoAccion.Manejo ? "ver `docs/conjuntos_manejo.md`" : "—";
                md.AppendLine("| " + Celda(a.descripcion) + " | " + a.tipo + " | " + a.peso + " | `" + a.idReferencia + "` | " + conj + " |  |  |");
                csv.AppendLine(Csv(c.idCaso, "3. Acciones críticas", a.idReferencia, a.descripcion, "tipo=" + a.tipo + "; peso=" + a.peso, a.tipo == TipoAccion.Manejo ? "ver docs/conjuntos_manejo.md" : "", "", "", ""));
            }
            md.AppendLine();

            // 4. Errores frecuentes
            md.AppendLine("### " + c.idCaso + " · 4. Errores frecuentes");
            md.AppendLine();
            md.AppendLine("| Descripción | idReferenciaCondicion | Retroalimentación | Detección | " + COL_VALID + " | " + COL_COMENT + " |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var e in c.erroresFrecuentes)
            {
                int m = Motivo(e);
                string deteccion = m == 0 ? "automática" : (m == 1 ? "**no se detecta automáticamente**" : "**no se detecta automáticamente** (formato no reconocido)");
                md.AppendLine("| " + Celda(e.descripcion) + " | " + (string.IsNullOrWhiteSpace(e.idReferenciaCondicion) ? "(vacía)" : "`" + e.idReferenciaCondicion + "`") + " | " + Celda(e.retroalimentacion) + " | " + deteccion + " |  |  |");
                csv.AppendLine(Csv(c.idCaso, "4. Errores frecuentes", e.idReferenciaCondicion, e.descripcion, e.retroalimentacion, string.IsNullOrWhiteSpace(e.idReferenciaCondicion) ? "(vacía)" : e.idReferenciaCondicion,
                                   m == 0 ? "detección automática" : "no se detecta automáticamente", "", ""));
                if (m != 0)
                {
                    mdErr.AppendLine("- **" + c.idCaso + "** — " + Celda(e.descripcion) + " · condición: " + (string.IsNullOrWhiteSpace(e.idReferenciaCondicion) ? "(vacía)" : "`" + e.idReferenciaCondicion + "`")
                                     + " · retroalimentación: " + Celda(e.retroalimentacion) + " · Validación: ____ · Condición propuesta: ____");
                    csvErr.AppendLine(Csv(c.idCaso, e.descripcion, e.idReferenciaCondicion ?? "", m == 1 ? "condición vacía" : "formato no reconocido", e.retroalimentacion, "", "", ""));
                }
            }
            md.AppendLine();

            // 5. Diagnóstico
            md.AppendLine("### " + c.idCaso + " · 5. Diagnóstico esperado");
            md.AppendLine();
            md.AppendLine("| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | " + COL_VALID + " | " + COL_COMENT + " |");
            md.AppendLine("|---|---|---|---|");
            md.AppendLine("| " + Celda(c.diagnosticoEsperado) + " | " + Celda(Conjuntos(c.conjuntosPalabrasClaveDiagnostico)) + " |  |  |");
            csv.AppendLine(Csv(c.idCaso, "5. Diagnóstico", "", c.diagnosticoEsperado, "", Conjuntos(c.conjuntosPalabrasClaveDiagnostico), "", "", ""));
            md.AppendLine();
            md.AppendLine("Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).");
            md.AppendLine();
        }

        // Resumen al final de la nota inicial (se inserta tras la cabecera)
        var resumenMd = new StringBuilder();
        resumenMd.AppendLine("## Resumen de conteos por caso");
        resumenMd.AppendLine();
        resumenMd.AppendLine("| Caso | Nombre | Preguntas | Hallazgos | Acciones críticas | Errores frecuentes | Conjuntos de diagnóstico |");
        resumenMd.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in resumen) resumenMd.AppendLine(r);
        resumenMd.AppendLine();

        string cuerpo = md.ToString();
        cuerpo = cuerpo.Replace("<!--RESUMEN-->\n", resumenMd.ToString().Replace("\r\n", "\n")).Replace("<!--RESUMEN-->\r\n", resumenMd.ToString());

        if (totalSinCond == 0) mdErr.AppendLine("(ningún error sin condición)");
        mdErr.AppendLine();
        mdErr.AppendLine("Total: " + totalSinCond + " errores sin condición válida.");

        File.WriteAllText(Path.Combine(docs, "validacion_clinica_flags.md"), Normalizar(cuerpo), utf8);
        File.WriteAllText(Path.Combine(docs, "validacion_clinica_flags.csv"), Normalizar(csv.ToString()), utf8Bom);
        File.WriteAllText(Path.Combine(docs, "errores_sin_condicion.md"), Normalizar(mdErr.ToString()), utf8);
        File.WriteAllText(Path.Combine(docs, "errores_sin_condicion.csv"), Normalizar(csvErr.ToString()), utf8Bom);
        File.WriteAllText(Path.Combine(docs, "README_validacion.md"), Normalizar(Readme(totalSinCond)), utf8);
        Debug.Log("[ValidacionClinica] Paquete generado en docs/: validacion_clinica_flags.md/.csv, errores_sin_condicion.md/.csv, README_validacion.md. Errores sin condición: " + totalSinCond);
    }

    // ── Cabecera ──────────────────────────────────────────────────────────────

    static void EscribirCabeceraFlags(StringBuilder md)
    {
        md.AppendLine("# Validación clínica — marcas de los 7 casos");
        md.AppendLine();
        md.AppendLine("Generado por `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` (borrador pendiente de validación clínica). Solo lee los assets; no modifica nada.");
        md.AppendLine();
        md.AppendLine("## Qué marcas se infirieron SIN validación clínica");
        md.AppendLine();
        md.AppendLine("Tres marcas de los casos se asignaron por inferencia, sin que un profesional las revisara. Cada una alimenta un componente del puntaje (100 puntos en Evaluación):");
        md.AppendLine();
        md.AppendLine("| Marca | Dónde está | Componente del puntaje que afecta | Cómo la usa el motor |");
        md.AppendLine("|---|---|---|---|");
        md.AppendLine("| `esPreguntaClave` | cada pregunta de anamnesis | **Anamnesis (15 puntos)** | puntaje = 15 × (preguntas clave realizadas ÷ preguntas clave totales) |");
        md.AppendLine("| `esHallazgoRelevante` | cada región del examen físico | **Examen físico (10 puntos)** | puntaje = 10 × (regiones relevantes examinadas ÷ regiones relevantes totales) |");
        md.AppendLine("| `esExamenPertinente` (hoy clasificación Pertinente / Indiferente / No pertinente) | cada examen, por caso | **Exámenes (20 puntos)** | pedir un pertinente suma; pedir uno no pertinente resta; los indiferentes no cuentan. **Se revisa en `docs/matriz_examenes.md`**, no aquí |");
        md.AppendLine();
        md.AppendLine("Además hay dos elementos con efecto en el puntaje que también están sin validar: los **conjuntos de palabras clave** (preguntas y diagnóstico, secciones 1 y 5 de cada caso; el Manejo está en `docs/conjuntos_manejo.md`) y los **errores frecuentes** (sección 4: cada uno detectado resta 5 puntos, tope 15).");
        md.AppendLine();
        md.AppendLine("Convención: `{a,b}` es UN conjunto (AND: todas las palabras deben aparecer en el texto del jugador); los conjuntos de una misma pregunta o acción son alternativas (OR). La comparación es por substring, en minúsculas y sin tildes. Las palabras de «legado» (palabras clave y sinónimos sueltos) se muestran con el prefijo `legado:`.");
        md.AppendLine();
        md.AppendLine("<!--RESUMEN-->");
    }

    static string Readme(int sinCond)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paquete de validación clínica — índice");
        sb.AppendLine();
        sb.AppendLine("Estos documentos se generan desde los assets del módulo de Razonamiento Clínico (borrador pendiente de validación clínica). Nada de lo que contienen ha sido validado por un profesional todavía.");
        sb.AppendLine();
        sb.AppendLine("| Documento | Qué contiene | Quién lo regenera |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine("| [validacion_clinica_flags.md](validacion_clinica_flags.md) / [.csv](validacion_clinica_flags.csv) | Por caso: preguntas, hallazgos, acciones críticas, errores frecuentes y diagnóstico, con las marcas inferidas (`esPreguntaClave`, `esHallazgoRelevante`) y dos columnas vacías para validar | `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` |");
        sb.AppendLine("| [conjuntos_manejo.md](conjuntos_manejo.md) | Los 14 conjuntos de palabras clave de las acciones de Manejo (7 casos × 2) y su frase de prueba | `Tools/Razonamiento Clínico/Conjuntos de Manejo/2. Generar docs/conjuntos_manejo.md` |");
        sb.AppendLine("| [matriz_examenes.md](matriz_examenes.md) / [.csv](matriz_examenes.csv) | Clasificación de cada examen del catálogo en cada caso (Pertinente / Indiferente / No pertinente) y los resultados | `Tools/Razonamiento Clínico/Catálogo de exámenes/3. Generar matriz de exámenes (docs/)` |");
        sb.AppendLine("| [errores_sin_condicion.md](errores_sin_condicion.md) / [.csv](errores_sin_condicion.csv) | Los " + sinCond + " errores frecuentes con condición vacía, que hoy no se detectan solos | `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` |");
        sb.AppendLine();
        sb.AppendLine("## Instrucciones para quien revisa");
        sb.AppendLine();
        sb.AppendLine("Empieza por la nota inicial de `validacion_clinica_flags.md`, que explica qué marcas se infirieron y cuántos puntos afecta cada una (anamnesis 15, examen físico 10, exámenes 20). En cada sección, escribe **OK** en la columna «Validación (OK / Cambiar)» si estás de acuerdo con la fila, o **Cambiar** si no, y anota en «Comentario» qué debería ser (por ejemplo, que una pregunta no es clave, que un hallazgo sí es relevante, o qué palabra falta en un conjunto). Revisa aparte la matriz de exámenes (¿es pertinente, indiferente o no pertinente pedir cada examen en cada caso?) y los conjuntos de Manejo, y para cada error de la lista sin condición propón qué conducta del jugador debería dispararlo. Puedes trabajar sobre los `.csv` (se abren en Excel) o sobre los `.md`; devuelve el archivo con las columnas completadas.");
        return sb.ToString();
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    // 0 = condición válida y reconocida; 1 = vacía; 2 = formato/prefijo no reconocido
    static int Motivo(ErrorFrecuente e)
    {
        string cond = e != null ? e.idReferenciaCondicion : null;
        if (string.IsNullOrWhiteSpace(cond)) return 1;
        int sep = cond.IndexOf(':');
        if (sep <= 0 || sep >= cond.Length - 1) return 2;
        string pref = cond.Substring(0, sep).Trim().ToLowerInvariant();
        return (pref == "omitio" || pref == "pregunto" || pref == "examino" || pref == "solicito") ? 0 : 2;
    }

    static string Conjuntos(List<ConjuntoPalabrasClave> lista)
    {
        if (lista == null || lista.Count == 0) return "";
        var partes = new List<string>();
        foreach (var c in lista)
            if (c != null && c.palabras != null && c.palabras.Count > 0) partes.Add("{" + string.Join(",", c.palabras.ToArray()) + "}");
        return string.Join(" ", partes.ToArray());
    }

    static string Legado(PreguntaAnamnesis p)
    {
        var partes = new List<string>();
        if (p.palabrasClave != null && p.palabrasClave.Count > 0) partes.Add("legado: " + string.Join(", ", p.palabrasClave.ToArray()));
        if (p.sinonimos != null && p.sinonimos.Count > 0) partes.Add("legado sinónimos: " + string.Join(", ", p.sinonimos.ToArray()));
        return string.Join(" · ", partes.ToArray());
    }

    // Celda de tabla Markdown: sin saltos de línea ni barras verticales.
    static string Celda(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\r\n", " ").Replace("\n", " ").Replace("|", "/");
    }

    static string Csv(params string[] campos)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < campos.Length; i++)
        {
            if (i > 0) sb.Append(',');
            string v = (campos[i] ?? "").Replace("\r\n", " ").Replace("\n", " ");
            sb.Append('"').Append(v.Replace("\"", "\"\"")).Append('"');
        }
        return sb.ToString();
    }

    // Fin de línea fijo (LF) para que regenerar sea byte-idéntico en cualquier máquina.
    static string Normalizar(string s) => s.Replace("\r\n", "\n");
}
#endif
