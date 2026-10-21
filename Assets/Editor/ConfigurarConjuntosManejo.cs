#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Carga los conjuntos de palabras clave de las 14 acciones de Manejo (7 casos x 2) y genera
/// docs/conjuntos_manejo.md. La tabla vive SOLO aquí (borrador pendiente de validación
/// clínica): Unity pierde los comentarios YAML al re-serializar los .asset, así que este
/// archivo y el documento generado son el registro.
/// Notación: conjuntos separados por " | "; palabras de un mismo conjunto unidas con "+" (AND).
/// Idempotente: reemplaza siempre la lista completa de la acción.
/// </summary>
public static class ConfigurarConjuntosManejo
{
    const string CARPETA = "Assets/Scripts/CasoClinico/FichasEjemplo/";

    const string OXIGENO_FOWLER =
        "oxigen+fowler | oxigen+semisent | oxigen+sentad | oxigen+sentar | oxigen+incorpor | oxigen+cabecera | " +
        "o2+fowler | o2+semisent | o2+sentad | o2+sentar | o2+incorpor | o2+cabecera";

    public struct Fila
    {
        public string asset, id, conjuntos, frase;
        public Fila(string a, string i, string c, string f) { asset = a; id = i; conjuntos = c; frase = f; }
    }

    public static readonly Fila[] FILAS =
    {
        new Fila("CasoSCA", "manejo_ecg_monitor_via",
            "ecg+monitor | ecg+venos | ecg+derivaciones | electrocardiograma+monitor | electrocardiograma+venos | electrocardiograma+derivaciones",
            "Tomar ECG de 12 derivaciones y monitorizar"),
        new Fila("CasoSCA", "manejo_activar_reperfusion",
            "reperfusion | trombolis | trombolit | fibrinolis | angioplast | hemodinamia | cateteris",
            "Activar reperfusión y trasladar a hemodinamia"),

        new Fila("CasoTBC", "manejo_aislamiento_respiratorio",
            "aisl | n95 | presion+negativa",
            "Aislamiento respiratorio y mascarilla N95"),
        new Fila("CasoTBC", "reconocer_notificacion_obligatoria",
            "notific+obligator | notific+autoridad | notific+seremi | notific+epidemiolog | declaracion+obligatoria",
            "Notificación obligatoria a la SEREMI"),

        new Fila("CasoEPA", "manejo_oxigeno_fowler", OXIGENO_FOWLER,
            "Oxígeno y posición semisentada"),
        new Fila("CasoEPA", "reconocer_causa_cardiogenica",
            "cardiogenic | insuficiencia+cardiac | sobrecarga+volumen | diuretic | furosemida | nitrato | nitroglicerina | restriccion+liquidos",
            "Diurético (furosemida) por sobrecarga de volumen"),

        new Fila("CasoEPOC", "manejo_o2_titulado",
            "oxigen+titul | oxigen+cautela | oxigen+controlad | oxigen+venturi | o2+titul | o2+cautela | o2+controlad | o2+venturi | venturi | 88+92",
            "Oxígeno titulado, saturación 88-92"),
        new Fila("CasoEPOC", "manejo_broncodilatadores",
            "broncodilat | salbutamol | ipratropio | nebuliz",
            "Broncodilatadores nebulizados"),

        new Fila("CasoNeumonia", "manejo_semifowler_o2", OXIGENO_FOWLER,
            "Oxígeno y semifowler"),
        new Fila("CasoNeumonia", "manejo_reconocer_gravedad",
            "gravedad | curb | hospitaliz | cuidados+intensiv | unidad+critic",
            "Evaluar gravedad con CURB-65 y hospitalizar"),

        new Fila("CasoSARS", "manejo_aislamiento_monitor_sato2",
            "aisl+saturacion | aisl+sato2 | aisl+spo2 | aisl+saturometr | aisl+oximetr | aisl+monitor",
            "Aislamiento y monitorización continua de SatO2"),
        new Fila("CasoSARS", "manejo_escalonado_via_aerea",
            "intub | ventilacion+mecanic | vmni | via+aerea | escalonad | alto+flujo",
            "Oxígeno y preparar intubación si empeora"),

        new Fila("CasoBronquitisAguda", "manejo_descartar_neumonia",
            "neumon | radiografia | consolidacion",
            "Descartar neumonía con radiografía de tórax"),
        new Fila("CasoBronquitisAguda", "manejo_educacion_alarma",
            "alarma | reconsult | automanejo | educ+paciente | volver+consultar",
            "Educar al paciente sobre signos de alarma y reconsultar"),
    };

    [MenuItem("Tools/Razonamiento Clínico/Conjuntos de Manejo/1. Cargar conjuntos en los 7 casos")]
    public static void Cargar()
    {
        int cargadas = 0;
        foreach (var f in FILAS)
        {
            var caso = AssetDatabase.LoadAssetAtPath<FichaCaso>(CARPETA + f.asset + ".asset");
            if (caso == null) { Debug.LogError("[ConjuntosManejo] No existe " + f.asset); continue; }
            AccionCritica accion = null;
            foreach (var a in caso.accionesCriticas)
                if (a.tipo == TipoAccion.Manejo && a.idReferencia == f.id) { accion = a; break; }
            if (accion == null) { Debug.LogError("[ConjuntosManejo] " + f.asset + " no tiene la acción " + f.id); continue; }

            accion.conjuntosPalabrasClave = new List<ConjuntoPalabrasClave>();
            foreach (var texto in f.conjuntos.Split('|'))
            {
                var conjunto = new ConjuntoPalabrasClave();
                foreach (var p in texto.Trim().Split('+')) conjunto.palabras.Add(p.Trim());
                accion.conjuntosPalabrasClave.Add(conjunto);
            }
            EditorUtility.SetDirty(caso);
            cargadas++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ConjuntosManejo] Acciones cargadas: " + cargadas + " / " + FILAS.Length);
    }

    [MenuItem("Tools/Razonamiento Clínico/Conjuntos de Manejo/2. Generar docs/conjuntos_manejo.md")]
    public static void GenerarDocumento()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Conjuntos de palabras clave — acciones de Manejo");
        sb.AppendLine();
        sb.AppendLine("> **Borrador pendiente de validación clínica.** Generado desde `Assets/Editor/ConfigurarConjuntosManejo.cs`; este archivo es el registro porque Unity pierde los comentarios YAML al re-serializar los .asset.");
        sb.AppendLine();
        sb.AppendLine("Reglas: `{a, b}` es UN conjunto (AND: todas las palabras deben aparecer). Los conjuntos de una acción son alternativas (OR). Coincidencia por substring, tras normalizar (minúsculas y sin tildes) el texto del jugador y las palabras de la ficha.");
        sb.AppendLine();
        sb.AppendLine("| Caso | idReferencia | Descripción | Conjuntos | Frase de prueba |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var f in FILAS)
        {
            var caso = AssetDatabase.LoadAssetAtPath<FichaCaso>(CARPETA + f.asset + ".asset");
            string desc = "";
            if (caso != null)
                foreach (var a in caso.accionesCriticas)
                    if (a.tipo == TipoAccion.Manejo && a.idReferencia == f.id) desc = a.descripcion.Replace("\n", " ").Replace("|", "/");
            var cs = new List<string>();
            foreach (var c in f.conjuntos.Split('|')) cs.Add("{" + c.Trim().Replace("+", ", ") + "}");
            sb.AppendLine("| " + (caso != null ? caso.idCaso : f.asset) + " | `" + f.id + "` | " + desc + " | " + string.Join(" ", cs.ToArray()) + " | «" + f.frase + "» |");
        }
        sb.AppendLine();
        sb.AppendLine("Falsos positivos conocidos: `o2` aparece dentro de «co2» y `gravedad` dentro de «sin gravedad» (el matching es por substring, sin negación).");

        string ruta = Path.Combine(Application.dataPath, "../docs/conjuntos_manejo.md");
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[ConjuntosManejo] Documento generado: docs/conjuntos_manejo.md");
    }
}
#endif
