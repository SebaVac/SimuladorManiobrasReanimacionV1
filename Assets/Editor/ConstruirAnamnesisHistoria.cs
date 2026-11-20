#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Anamnesis mejorada del módulo de Razonamiento Clínico (herramientas de Editor, no se ejecutan en el visor):
///  1. Crea el CatalogoIntenciones, llena la HistoriaPaciente de los 7 casos SOLO con datos ya presentes en cada ficha
///     (lo demás queda en "No refiere"), aplica el mapeo intención → pregunta clave, fija la respuesta genérica y asigna el
///     catálogo al GestorCasoClinico de SceneDiagnostico.
///  2. Control anti-fuga: busca las palabras del propio diagnóstico en la historia, las respuestas y los textos del catálogo.
///  3. Genera docs/historia_paciente_pendientes.md y docs/validacion_historia_paciente.md/.csv.
/// Idempotente: se puede volver a ejecutar.
/// </summary>
public static class ConstruirAnamnesisHistoria
{
    const string RUTA_CATALOGO = "Assets/Scripts/CasoClinico/CatalogoIntenciones.asset";
    const string DIR_FICHAS = "Assets/Scripts/CasoClinico/FichasEjemplo/";
    const string RUTA_ESCENA = "Assets/Scenes/SceneDiagnostico.unity";
    const string FALLBACK = "Disculpe, no le entendí bien. ¿Me lo puede preguntar de otra forma?";
    const string NR = HistoriaPaciente.NO_REFIERE;

    // ── Catálogo ─────────────────────────────────────────────────────────────────────────────────
    // id | descripción | campo | conjuntos ("a,b|c,d" = {a AND b} OR {c AND d}) | excluyentes (mismo formato)
    // Raíces sin tildes y en minúscula. Las raíces cortas (< 5 letras) solo coinciden exactas (sin tolerancia de tipeo).
    static readonly string[][] CATALOGO =
    {
        new[] { "tiempo_evolucion", "Cuánto tiempo hace / desde cuándo empezó el problema.", "Inicio",
                "hace,cuant|desde,cuand|cuand,empez|cuand,comenz|cuand,inici|cuand,parti|cuand,aparec|cuand,cuenta|cuant,tiempo|cuant,llev|tiempo,llev|cuant,dias|cuant,horas|cuant,semanas|momento,empez|momento,comenz|hace,tiempo|hace,mucho,que|desde,hace",
                "fum|cigarr|tabac|alcohol|trago|copete|remedi|medicament|pastill|trabaj|vacun|operad|alerg|ejercic|deport|vive|hospital|esper" },
        new[] { "intensidad", "Qué tan fuerte es el dolor o la molestia.", "Intensidad",
                "cuant,duel|cuant,dol|cuant,molest|fuert|intens|escala|nota,dol|soportab|duel,mucho|dol,mucho|molest,mucho|tan,mal|grave|hart,duel|hart,dol|hart,molest",
                "hace|desde|tiempo|llev|dias|horas|semanas|meses|anos" },
        new[] { "localizacion", "Dónde le duele o dónde siente la molestia.", "Localizacion",
                "donde,duel|donde,dol|donde,molest|donde,siente|que,parte|lado,duel|lado,dol|parte,duel|parte,dol|parte,molest|parte,pecho|zona|ubic|localiz|mostr,donde|donde,exact|corre|irradia|extiende|lugar,dol|lugar,duel|lugar,molest",
                "aparte|ademas" },
        new[] { "empeora", "Qué hace que empeore.", "Empeora",
                "empeor|agrav|peor|aument,dol|aument,molest|provo|desencaden|gatill|fuert,cuando|intensific",
                "" },
        new[] { "alivia", "Qué hace que mejore o se calme.", "Alivia",
                "mejor|alivi|calma|cede,repos|cede,dol|cede,algo|cede,descans|quita|disminu|baja,dol|baja,molest|baje,dol|baje,molest|repos|posicion|se,le,pase",
                "" },
        new[] { "remedios", "Qué remedios o medicamentos toma o ha tomado.", "Remedios",
                "remedi|medicament|medicin|medicac|pastill|tomando|farmac|analgesic|paracetamol|ibuprofen|automedic|tratamiento|toma,algo,para|tomo,algo,para|tomad,algo",
                "alerg|reaccion|cae,mal" },
        new[] { "alergias", "Si es alérgico a algo.", "Alergias",
                "alerg|intoleranc|reaccion,medicament|reaccion,alimento|cae,mal,remedi|cae,mal,medicament|cae,mal,alimento",
                "" },
        new[] { "antecedentes", "Enfermedades, operaciones u hospitalizaciones previas.", "Antecedentes",
                "antecedent|enfermedad|cronic|operaci|operad|cirugi|hospital|padece|sufre,de,algo|patolog|diabet|hipertens|presion,alta|colesterol|problema,salud|historial|historia,medica|historia,clinica|parecido,antes|diagnostic,previ",
                "" },
        new[] { "habitos", "Hábitos: tabaco, alcohol, drogas, ejercicio, alimentación.", "Habitos",
                "fum|cigarr|tabac|alcohol|trago|copete|bebid|drog|ejercic|deport|habito|estilo,vida|dieta|alimentac|consume|vicio",
                "" },
        new[] { "sintomas_asociados", "Otros síntomas que acompañan al problema.", "SintomasAsociados",
                "sintoma|algo,mas|ademas|acompan,algo|acompan,otr|tambien,siente|tambien,tiene|otra,molest|otra,cosa|otro,problema|otro,malestar|que,mas,siente",
                "" },
        new[] { "motivo_consulta", "Qué le pasa / qué lo trae (responde con el motivo de consulta de la ficha).", "MotivoConsulta",
                "que,le,pasa|molest,algo|que,molest|motivo|trae,aqui|como,siente|que,siente|ayud|consulta|que,duel|cual,problema|que,problema|que,trae|sucede|como,encuentra|que,le,paso|que,vino|ocurre",
                "cuesta|cobr|precio|valor" },
    };

    // ── Historia del paciente: campo → (texto, fuente). Solo datos ya presentes en la ficha. ─────────────────
    // Orden: inicio, evolucion, empeora, alivia, remedios, alergias, antecedentes, habitos, intensidad, localizacion,
    // sintomasAsociados, perfil. Cada celda: "texto" o "No refiere"; la fuente va en el arreglo paralelo.
    static readonly Dictionary<string, string[]> HISTORIAS = new Dictionary<string, string[]>
    {
        { "CASO-BRONQ", new[] {
            "Empecé con tos hace unos cinco días.",
            "Al principio la tos era seca y ahora sale con flema clara.",
            "Cuando toso se me tensa el pecho.",
            NR, NR, NR,
            "Fumo y hace una semana andaba con un resfrío.",
            "Fumo desde hace varios años.",
            NR,
            "Me duele el pecho.",
            "Un poco de fiebre.",
            "Soy Patricia González, tengo 41 años." } },
        { "CASO-EPA", new[] {
            "Hace unas dos horas, y empeoró rápido.",
            "Fue empeorando rápido desde que empezó.",
            "Me ahogo más cuando me acuesto; tengo que dormir sentada con varias almohadas.",
            NR,
            "Tengo un diurético, pero estos últimos días se me olvidó tomarlo.",
            NR,
            "Tengo problemas del corazón y la presión alta.",
            NR,
            "Me cuesta mucho respirar, siento que me ahogo.",
            NR,
            "Tengo tos con una flema rosada y espumosa, y se me hincharon harto los pies.",
            "Soy Elena Sepúlveda, tengo 68 años." } },
        { "CASO-EPOC", new[] {
            "Llevo años con tos; ahora me cuesta respirar mucho más que antes.",
            "Se me agravó; ya no puedo subir escaleras como antes.",
            "Me canso más cuando hago esfuerzo; ya no puedo subir escaleras como antes.",
            NR, NR, NR,
            "Esta es como la tercera vez que me pasa esto.",
            "Fumé como 30 años, un paquete al día.",
            "Me cuesta respirar mucho más que antes.",
            NR,
            "Boto harta flema en las mañanas y he bajado un poco de peso.",
            "Soy Manuel Rojas, tengo 64 años." } },
        { "CASO-NAC", new[] {
            "Desde ayer, con fiebre muy alta y calofríos.",
            NR,
            "Me duele más cuando respiro hondo.",
            NR, NR, NR,
            "Tengo EPOC y no he estado hospitalizado hace poco.",
            NR,
            "Tengo un dolor fuerte en el pecho y la fiebre es muy alta.",
            "Un dolor punzante en el pecho, de este lado.",
            "Tengo tos con flema amarillo-verdosa, calofríos y respiro rápido y corto.",
            "Soy Eduardo Fernández, tengo 72 años." } },
        { "CASO-SARS", new[] {
            "Hace unos días que me cuesta respirar.",
            NR,
            "Me cuesta más respirar cuando camino.",
            NR, NR, NR,
            "No tengo otras enfermedades. Volví hace una semana de un viaje donde había un brote.",
            NR,
            "La cabeza me duele harto.",
            "Me duele todo el cuerpo, sobre todo las piernas, y también la cabeza.",
            "Tengo fiebre como de 38 grados con calofríos, dolor de cabeza y un poco de tos seca.",
            "Soy Valentina Torres, tengo 29 años." } },
        { "CASO-SCA", new[] {
            "Hace unos 30 minutos, de repente.",
            NR,
            NR,
            "No se me pasa; sigue igual aunque estoy sentado.",
            NR, NR,
            "Soy hipertenso y diabético, tengo alterados los lípidos y tuve un problema del corazón hace dos años.",
            "Fumo.",
            "Es un dolor fuerte.",
            "En el pecho, y se me corre al brazo izquierdo y al cuello.",
            "Estoy sudando frío y con ganas de vomitar.",
            "Soy Jorge Alarcón, tengo 59 años." } },
        { "CASO-TBC", new[] {
            "Como tres semanas con la tos.",
            "Antes era seca y ahora sale con flema.",
            NR, NR, NR, NR,
            "Estuve en contacto con un vecino que estuvo enfermo hace unos meses, y somos siete en una casa chica.",
            NR, NR, NR,
            "He bajado unos cinco kilos sin hacer dieta, a veces empapo las sábanas en la noche, me siento afiebrado en las tardes y un par de veces vi hilos de sangre.",
            "Soy Carlos Muñoz, tengo 34 años." } },
    };

    // Fuente de cada campo (para el documento de validación). Mismo orden que HISTORIAS.
    static readonly Dictionary<string, string[]> FUENTES = new Dictionary<string, string[]>
    {
        { "CASO-BRONQ", new[] { "respuesta bronq_inicio_tos; motivoConsulta", "respuesta bronq_tos_seca_a_productiva", "motivoConsulta; respuesta bronq_dolor_toracico", "", "", "", "antecedentes (tabaquismo; resfrío reciente); respuesta bronq_exposicion", "respuesta bronq_tabaquismo", "", "motivoConsulta", "respuesta bronq_fiebre", "datosAdministrativos" } },
        { "CASO-EPA", new[] { "respuesta epa_inicio_disnea", "respuesta epa_inicio_disnea", "motivoConsulta; respuesta epa_ortopnea", "", "respuesta epa_adherencia; antecedentes (mal apego al diurético)", "", "antecedentes (insuficiencia cardíaca, hipertensión) dicho sin nombrar el diagnóstico", "", "motivoConsulta", "", "respuesta epa_tos; respuesta epa_edema", "datosAdministrativos" } },
        { "CASO-EPOC", new[] { "motivoConsulta", "motivoConsulta; respuesta epoc_disnea_esfuerzo", "respuesta epoc_disnea_esfuerzo", "", "", "", "antecedentes (exacerbaciones previas); respuesta epoc_exacerbaciones", "respuesta epoc_tabaquismo", "motivoConsulta", "", "respuesta epoc_expectoracion; respuesta epoc_baja_peso", "datosAdministrativos" } },
        { "CASO-NAC", new[] { "respuesta nac_inicio_fiebre", "", "motivoConsulta", "", "", "", "respuesta nac_comorbilidad; antecedentes (sin hospitalización reciente)", "", "motivoConsulta; respuesta nac_inicio_fiebre", "respuesta nac_dolor_pleuritico", "respuesta nac_tos_productiva; nac_inicio_fiebre; nac_disnea", "datosAdministrativos" } },
        { "CASO-SARS", new[] { "motivoConsulta", "", "respuesta sars_disnea", "", "", "", "antecedentes (sin comorbilidades); respuesta sars_nexo", "", "respuesta sars_cefalea", "respuesta sars_mialgias; sars_cefalea", "respuesta sars_fiebre; sars_cefalea; sars_tos", "datosAdministrativos" } },
        { "CASO-SCA", new[] { "respuesta sca_inicio_dolor", "", "", "respuesta sca_relacion_reposo", "", "", "respuesta sca_factores_riesgo; antecedentes (dislipidemia; infarto previo dicho sin nombrar el diagnóstico)", "respuesta sca_factores_riesgo (fuma)", "motivoConsulta (dolor fuerte)", "motivoConsulta; respuesta sca_irradiacion", "respuesta sca_sintomas_asociados", "datosAdministrativos" } },
        { "CASO-TBC", new[] { "respuesta tbc_duracion_tos", "respuesta tbc_duracion_tos", "", "", "", "", "antecedentes (contacto reciente; hacinamiento) sin nombrar el diagnóstico; respuesta tbc_contacto; tbc_hacinamiento", "", "", "", "respuesta tbc_baja_peso; tbc_sudor_nocturno; tbc_fiebre; tbc_hemoptisis", "datosAdministrativos" } },
    };

    static readonly string[] CAMPOS = { "inicio", "evolucion", "empeora", "alivia", "remedios", "alergias", "antecedentes", "habitos", "intensidad", "localizacion", "sintomasAsociados", "perfil" };

    // Mapeo aplicado: (idCaso, idPregunta) → intenciones. SOLO equivalencias evidentes por el texto de la pregunta.
    static readonly string[][] MAPEO_APLICADO =
    {
        new[] { "CASO-SCA", "sca_inicio_dolor", "tiempo_evolucion" },
        new[] { "CASO-BRONQ", "bronq_inicio_tos", "tiempo_evolucion" },
        new[] { "CASO-EPA", "epa_inicio_disnea", "tiempo_evolucion" },
        new[] { "CASO-NAC", "nac_inicio_fiebre", "tiempo_evolucion" },
        new[] { "CASO-EPOC", "epoc_duracion_tos", "tiempo_evolucion" },
        new[] { "CASO-TBC", "tbc_duracion_tos", "tiempo_evolucion" },
        new[] { "CASO-SCA", "sca_sintomas_asociados", "sintomas_asociados" },
    };

    // Mapeos propuestos y NO aplicados (duda razonable): se listan para que los revise un profesional.
    static readonly string[][] MAPEO_PROPUESTO =
    {
        new[] { "CASO-SCA", "sca_relacion_reposo", "alivia", "La pregunta es solo por el reposo; \"qué lo alivia\" es más amplia." },
        new[] { "CASO-EPA", "epa_ortopnea", "empeora", "La pregunta es solo por acostarse; \"qué lo empeora\" es más amplia." },
        new[] { "CASO-BRONQ", "bronq_tabaquismo", "habitos", "\"Hábitos\" incluye alcohol, ejercicio, etc.; la pregunta es solo por fumar." },
        new[] { "CASO-EPOC", "epoc_tabaquismo", "habitos", "Igual que arriba." },
        new[] { "CASO-SCA", "sca_antecedente_cardiaco", "antecedentes", "La pregunta es solo por el corazón; \"antecedentes\" abarca más." },
        new[] { "CASO-EPA", "epa_cardiopatia", "antecedentes", "Igual que arriba." },
        new[] { "CASO-NAC", "nac_comorbilidad", "antecedentes", "\"Enfermedad crónica\" es un subconjunto de antecedentes." },
        new[] { "CASO-EPOC", "epoc_exacerbaciones", "antecedentes", "\"Episodios similares\" es un subconjunto de antecedentes." },
        new[] { "CASO-SCA", "sca_irradiacion", "localizacion", "La pregunta es por la irradiación, no por dónde duele." },
    };

    // ── 1. Crear ─────────────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Razonamiento Clínico/Anamnesis/1. Crear catálogo, historias y mapeo")]
    public static void Crear()
    {
        var cat = AssetDatabase.LoadAssetAtPath<CatalogoIntenciones>(RUTA_CATALOGO);
        if (cat == null) { cat = ScriptableObject.CreateInstance<CatalogoIntenciones>(); AssetDatabase.CreateAsset(cat, RUTA_CATALOGO); }
        cat.intenciones = new List<IntencionConsulta>();
        foreach (var d in CATALOGO)
        {
            var it = new IntencionConsulta { id = d[0], descripcion = d[1], campoHistoria = (CampoHistoria)System.Enum.Parse(typeof(CampoHistoria), d[2]) };
            it.conjuntos = Conjuntos(d[3]);
            it.excluyentes = Conjuntos(d[4]);
            cat.intenciones.Add(it);
        }
        cat.respuestaSinDato = "Mmm, no sabría decirle.";
        foreach (var it in cat.intenciones) it.ultimoRecurso = it.id == "motivo_consulta";   // genérica: solo si ninguna otra coincide
        EditorUtility.SetDirty(cat);

        int casos = 0, mapeos = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:FichaCaso", new[] { "Assets/Scripts/CasoClinico/FichasEjemplo" }))
        {
            var f = AssetDatabase.LoadAssetAtPath<FichaCaso>(AssetDatabase.GUIDToAssetPath(guid));
            f.respuestaGenericaSinMatch = FALLBACK;
            if (HISTORIAS.TryGetValue(f.idCaso, out var h))
            {
                f.historia = new HistoriaPaciente
                {
                    inicio = h[0], evolucion = h[1], empeora = h[2], alivia = h[3], remedios = h[4], alergias = h[5],
                    antecedentes = h[6], habitos = h[7], intensidad = h[8], localizacion = h[9], sintomasAsociados = h[10], perfil = h[11],
                };
                casos++;
            }
            AmpliarConjuntos(f);
            AplicarCorrecciones(f);
            foreach (var p in f.preguntasClave) if (p != null) p.intencionesQueSatisface = new List<string>();
            foreach (var m in MAPEO_APLICADO)
            {
                if (m[0] != f.idCaso) continue;
                var p = f.preguntasClave.Find(x => x != null && x.idPregunta == m[1]);
                if (p == null) { Debug.LogError("[Anamnesis] pregunta " + m[1] + " no existe en " + f.idCaso); continue; }
                if (!p.intencionesQueSatisface.Contains(m[2])) p.intencionesQueSatisface.Add(m[2]);
                mapeos++;
            }
            EditorUtility.SetDirty(f);
        }
        AssetDatabase.SaveAssets();

        // Asignar el catálogo al GestorCasoClinico de SceneDiagnostico.
        var sc = EditorSceneManager.OpenScene(RUTA_ESCENA, OpenSceneMode.Single);
        var g = Object.FindFirstObjectByType<GestorCasoClinico>(FindObjectsInactive.Include);
        var so = new SerializedObject(g);
        so.FindProperty("catalogoIntenciones").objectReferenceValue = cat;
        so.FindProperty("registrarSinReconocer").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log("[Anamnesis] catálogo con " + cat.intenciones.Count + " intenciones; historia en " + casos + " casos; " + mapeos + " mapeos aplicados; catálogo asignado al GestorCasoClinico de SceneDiagnostico.");
    }

    // ── Correcciones del control anti-fuga ───────────────────────────────────────────────────────────
    // Respuestas YA EXISTENTES de preguntas clave que nombraban el diagnóstico del caso. Se reescriben conservando lo que el
    // paciente sabe sin decir la palabra, y quedan marcadas como "modificadas" en docs/validacion_historia_paciente.md para que
    // un profesional las revise (la redacción original está en la columna "Antes").
    static readonly string[][] CORRECCIONES_FUGA =
    {
        // idCaso, idPregunta, texto original, texto nuevo
        new[] { "CASO-EPA", "epa_cardiopatia", "Sí, tengo insuficiencia cardíaca.", "Sí, ya me habían dicho que tengo problemas del corazón." },
        new[] { "CASO-SCA", "sca_antecedente_cardiaco", "Tuve un infarto hace dos años.", "Tuve un problema del corazón hace dos años." },
        new[] { "CASO-TBC", "tbc_contacto", "Mi vecino tuvo tuberculosis hace unos meses.", "Mi vecino estuvo enfermo hace unos meses." },
    };

    static void AplicarCorrecciones(FichaCaso f)
    {
        foreach (var c in CORRECCIONES_FUGA)
        {
            if (c[0] != f.idCaso) continue;
            var p = f.preguntasClave.Find(x => x != null && x.idPregunta == c[1]);
            if (p != null && (p.respuestaPaciente == c[2] || p.respuestaPaciente == c[3])) p.respuestaPaciente = c[3];
            else Debug.LogWarning("[Anamnesis] la respuesta de " + c[1] + " no coincide con la esperada; no se corrige: '" + (p != null ? p.respuestaPaciente : "?") + "'");
        }
    }

    // ── Cobertura de vocabulario de las preguntas clave ─────────────────────────────────────────────
    // "dol" no es subcadena de "duele/duelen": todo conjunto con "dol" recibe un gemelo con "duel" (misma pregunta dicha como
    // "¿le duele...?"). Más extras puntuales (idCaso, idPregunta, conjunto) para sugeridas que no se reconocían a sí mismas.
    static readonly string[][] CONJUNTOS_EXTRA =
    {
        new[] { "CASO-EPA", "epa_ortopnea", "acost,dificult" },
        new[] { "CASO-EPA", "epa_ortopnea", "acost,empeor" },
    };

    static bool MismoConjunto(ConjuntoPalabrasClave c, string[] palabras)
    {
        if (c.palabras.Count != palabras.Length) return false;
        for (int i = 0; i < palabras.Length; i++) if (c.palabras[i] != palabras[i]) return false;
        return true;
    }

    /// <summary>Conjuntos que esta herramienta agrega a las preguntas clave: (idCaso, idPregunta, conjunto).</summary>
    public static List<string[]> ConjuntosAgregados(FichaCaso f)
    {
        var lista = new List<string[]>();
        foreach (var p in f.preguntasClave)
        {
            if (p == null) continue;
            foreach (var c in p.conjuntosPalabrasClave)
            {
                if (c.palabras.Contains("dol")) lista.Add(new[] { f.idCaso, p.idPregunta, string.Join(",", c.palabras.ConvertAll(w => w == "dol" ? "duel" : w).ToArray()) });
            }
        }
        foreach (var e in CONJUNTOS_EXTRA) if (e[0] == f.idCaso) lista.Add(e);
        return lista;
    }

    static int AmpliarConjuntos(FichaCaso f)
    {
        int n = 0;
        foreach (var a in ConjuntosAgregados(f))
        {
            var p = f.preguntasClave.Find(x => x != null && x.idPregunta == a[1]);
            var palabras = a[2].Split(',');
            if (p == null || p.conjuntosPalabrasClave.Exists(c => MismoConjunto(c, palabras))) continue;
            p.conjuntosPalabrasClave.Add(new ConjuntoPalabrasClave { palabras = new List<string>(palabras) });
            n++;
        }
        return n;
    }

    static List<ConjuntoPalabrasClave> Conjuntos(string def)
    {
        var lista = new List<ConjuntoPalabrasClave>();
        if (string.IsNullOrEmpty(def)) return lista;
        foreach (var set in def.Split('|'))
            lista.Add(new ConjuntoPalabrasClave { palabras = new List<string>(set.Split(',')) });
        return lista;
    }

    // ── 2. Control anti-fuga ─────────────────────────────────────────────────────────────────────

    // Palabras del diagnóstico que no lo delatan por sí solas (genéricas).
    static readonly HashSet<string> NO_DELATORAS = new HashSet<string> {
        "de", "la", "el", "los", "las", "del", "con", "en", "y", "o", "a", "por", "para", "origen", "agudo", "aguda", "grave", "activa",
        "comunidad", "adquirida", "caso", "ejemplo", "placeholder", "diagnostico", "pulmonar", "cronica", "tipo", "descompensacion", "enfermedad", "enfermedades",
    };

    [MenuItem("Tools/Razonamiento Clínico/Anamnesis/2. Control anti-fuga (historia, respuestas y catálogo)")]
    public static void ControlAntiFuga()
    {
        string informe = EjecutarControl(out int hallazgos);
        string ruta = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/antifuga_historia_paciente.txt"));
        File.WriteAllText(ruta, informe, new UTF8Encoding(false));
        Debug.Log("[Anamnesis] control anti-fuga: " + hallazgos + " coincidencia(s). Informe en docs/antifuga_historia_paciente.txt\n" + informe);
    }

    public static string EjecutarControl(out int hallazgos)
    {
        hallazgos = 0;
        var sb = new StringBuilder();
        sb.AppendLine("Control anti-fuga: busca las palabras del propio diagnóstico (diagnosticoEsperado, nombreCaso y todas las palabras de");
        sb.AppendLine("conjuntosPalabrasClaveDiagnostico, sin tildes) en: historia del paciente, respuestaPaciente de cada pregunta, respuesta genérica,");
        sb.AppendLine("motivo de consulta (lo responde la intención motivo_consulta) y los textos del catálogo. Coincidencia = palabra entera igual, o");
        sb.AppendLine("palabra del texto que empieza por una raíz de 6+ letras del diagnóstico. Palabras genéricas ignoradas: " + string.Join(", ", new List<string>(NO_DELATORAS).ToArray()));
        sb.AppendLine();
        var cat = AssetDatabase.LoadAssetAtPath<CatalogoIntenciones>(RUTA_CATALOGO);
        foreach (var guid in AssetDatabase.FindAssets("t:FichaCaso", new[] { "Assets/Scripts/CasoClinico/FichasEjemplo" }))
        {
            var f = AssetDatabase.LoadAssetAtPath<FichaCaso>(AssetDatabase.GUIDToAssetPath(guid));
            if (f.idCaso == "CASO-000") continue;   // placeholder
            var raices = new HashSet<string>();
            AgregarPalabras(raices, f.diagnosticoEsperado); AgregarPalabras(raices, f.nombreCaso);
            foreach (var c in f.conjuntosPalabrasClaveDiagnostico) foreach (var p in c.palabras) AgregarPalabras(raices, p);
            raices.RemoveWhere(r => NO_DELATORAS.Contains(r));
            sb.AppendLine("== " + f.idCaso + " (" + f.nombreCaso + ") | palabras vigiladas: " + string.Join(", ", new List<string>(raices).ToArray()));
            var textos = new List<string[]>();   // {origen, texto}
            if (f.historia != null)
                foreach (var campo in CAMPOS) textos.Add(new[] { "historia." + campo, Campo(f.historia, campo) });
            textos.Add(new[] { "motivoConsulta", f.motivoConsulta });
            textos.Add(new[] { "respuestaGenericaSinMatch", f.respuestaGenericaSinMatch });
            foreach (var p in f.preguntasClave) if (p != null) textos.Add(new[] { "respuesta " + p.idPregunta, p.respuestaPaciente });
            int n = 0;
            foreach (var t in textos)
                foreach (var r in raices)
                {
                    string hallado = Coincide(t[1], r);
                    if (hallado == null) continue;
                    n++; hallazgos++;
                    sb.AppendLine("   COINCIDE [" + t[0] + "] palabra '" + hallado + "' (diagnóstico: '" + r + "') en: " + t[1]);
                }
            if (n == 0) sb.AppendLine("   sin coincidencias");
        }
        sb.AppendLine("== Catálogo (descripciones y respuestaSinDato; aplica a todos los casos)");
        int nc = 0;
        if (cat != null)
        {
            var todas = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:FichaCaso", new[] { "Assets/Scripts/CasoClinico/FichasEjemplo" }))
            {
                var f = AssetDatabase.LoadAssetAtPath<FichaCaso>(AssetDatabase.GUIDToAssetPath(guid));
                if (f.idCaso == "CASO-000") continue;
                var raices = new HashSet<string>();
                AgregarPalabras(raices, f.diagnosticoEsperado); AgregarPalabras(raices, f.nombreCaso);
                foreach (var c in f.conjuntosPalabrasClaveDiagnostico) foreach (var p in c.palabras) AgregarPalabras(raices, p);
                raices.RemoveWhere(r => NO_DELATORAS.Contains(r));
                foreach (var it in cat.intenciones)
                    foreach (var r in raices)
                    {
                        string h = Coincide(it.descripcion, r);
                        if (h != null) { nc++; hallazgos++; sb.AppendLine("   COINCIDE [catálogo " + it.id + ".descripcion] '" + h + "' (diagnóstico de " + f.idCaso + ": '" + r + "') en: " + it.descripcion); }
                    }
                string h2 = null; foreach (var r in raices) { h2 = Coincide(cat.respuestaSinDato, r); if (h2 != null) { nc++; hallazgos++; sb.AppendLine("   COINCIDE [catálogo.respuestaSinDato] '" + h2 + "' en: " + cat.respuestaSinDato); } }
            }
        }
        if (nc == 0) sb.AppendLine("   sin coincidencias");
        sb.AppendLine();
        sb.AppendLine("TOTAL: " + hallazgos + " coincidencia(s).");
        return sb.ToString();
    }

    static string Campo(HistoriaPaciente h, string campo)
    {
        switch (campo)
        {
            case "inicio": return h.inicio; case "evolucion": return h.evolucion; case "empeora": return h.empeora; case "alivia": return h.alivia;
            case "remedios": return h.remedios; case "alergias": return h.alergias; case "antecedentes": return h.antecedentes; case "habitos": return h.habitos;
            case "intensidad": return h.intensidad; case "localizacion": return h.localizacion; case "sintomasAsociados": return h.sintomasAsociados;
            default: return h.perfil;
        }
    }

    static void AgregarPalabras(HashSet<string> set, string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        foreach (var w in Tokens(texto)) if (w.Length >= 3) set.Add(w);
    }

    static string Coincide(string texto, string raiz)
    {
        foreach (var w in Tokens(texto))
        {
            if (w == raiz) return w;
            if (raiz.Length >= 6 && w.StartsWith(raiz)) return w;
            if (w.Length >= 6 && raiz.StartsWith(w) && w.Length >= raiz.Length - 2) return w;
        }
        return null;
    }

    static IEnumerable<string> Tokens(string texto)
    {
        string d = (texto ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in d)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        foreach (var w in sb.ToString().Split(' ')) if (w.Length > 0) yield return w;
    }

    // ── 3. Documentos ────────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Razonamiento Clínico/Anamnesis/3. Generar documentos de validación (docs/)")]
    public static void GenerarDocumentos()
    {
        string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs"));
        var cat = AssetDatabase.LoadAssetAtPath<CatalogoIntenciones>(RUTA_CATALOGO);
        var fichas = new List<FichaCaso>();
        foreach (var guid in AssetDatabase.FindAssets("t:FichaCaso", new[] { "Assets/Scripts/CasoClinico/FichasEjemplo" }))
        {
            var f = AssetDatabase.LoadAssetAtPath<FichaCaso>(AssetDatabase.GUIDToAssetPath(guid));
            if (f.idCaso != "CASO-000") fichas.Add(f);
        }
        fichas.Sort((a, b) => string.CompareOrdinal(a.idCaso, b.idCaso));

        // pendientes
        var pend = new StringBuilder();
        pend.AppendLine("# Historia del paciente — campos pendientes de validación");
        pend.AppendLine();
        pend.AppendLine("Generado por `Tools/Razonamiento Clínico/Anamnesis/3. Generar documentos de validación (docs/)`. La historia de cada caso se escribió **solo con datos ya presentes en la ficha** (motivo de consulta, antecedentes, respuestas a las preguntas clave y datos administrativos). Todo campo sin fuente en la ficha quedó en **\"No refiere\"**: cuando el estudiante pregunta por él, el paciente responde \"Mmm, no sabría decirle.\" (no afirma ni niega nada). Un profesional debe decidir si completa cada uno o lo deja así.");
        pend.AppendLine();
        int totalPend = 0;
        foreach (var f in fichas)
        {
            var lista = new List<string>();
            foreach (var campo in CAMPOS) if (campo != "perfil" && HistoriaPaciente.EsNoRefiere(Campo(f.historia, campo))) lista.Add(campo);
            totalPend += lista.Count;
            pend.AppendLine("- **" + f.idCaso + " — " + f.nombreCaso + "** (" + lista.Count + " de 11): " + (lista.Count == 0 ? "ninguno" : string.Join(", ", lista.ToArray())));
        }
        pend.AppendLine();
        pend.AppendLine("Total: " + totalPend + " campos en \"No refiere\" (el campo `perfil` no cuenta: no lo contesta ninguna intención).");
        File.WriteAllText(Path.Combine(docs, "historia_paciente_pendientes.md"), pend.ToString(), new UTF8Encoding(false));

        // validación md + csv
        var md = new StringBuilder(); var csv = new StringBuilder();
        csv.AppendLine("\"Caso\",\"Sección\",\"Id / referencia\",\"Elemento\",\"Detalle\",\"Fuente / condición\",\"Validación (OK / Cambiar)\",\"Comentario\"");
        md.AppendLine("# Validación — historia del paciente, catálogo de intenciones y mapeo");
        md.AppendLine();
        md.AppendLine("Generado por `Tools/Razonamiento Clínico/Anamnesis/3. Generar documentos de validación (docs/)` (borrador pendiente de validación clínica). Solo lee los assets. Las columnas **Validación (OK / Cambiar)** y **Comentario** son para quien revise; el `.csv` trae las mismas filas.");
        md.AppendLine();
        md.AppendLine("Qué cambia con esto: cuando el estudiante escribe una pregunta que no coincide con ninguna pregunta clave del caso, el sistema busca la **intención** más específica del catálogo y el paciente responde con el campo correspondiente de su **historia**. **El puntaje no cambia**: solo puntúan las preguntas clave, y una intención puntúa únicamente si está vinculada a una pregunta clave (sección 3). El matching sigue siendo determinista (sin IA ni red).");
        md.AppendLine();
        md.AppendLine("## 1. Historia del paciente (respuestas en primera persona)");
        md.AppendLine();
        foreach (var f in fichas)
        {
            md.AppendLine("### " + f.idCaso + " — " + f.nombreCaso + " (" + f.datosAdministrativos.sexoPaciente + ", " + f.datosAdministrativos.edadPaciente + ")");
            md.AppendLine();
            md.AppendLine("| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |");
            md.AppendLine("|---|---|---|---|---|");
            for (int i = 0; i < CAMPOS.Length; i++)
            {
                string texto = Campo(f.historia, CAMPOS[i]);
                string fuente = HistoriaPaciente.EsNoRefiere(texto) ? "sin fuente en la ficha (pendiente)" : FUENTES[f.idCaso][i];
                md.AppendLine("| " + CAMPOS[i] + " | " + Esc(texto) + " | " + Esc(fuente) + " | | |");
                csv.AppendLine(Csv(f.idCaso) + "," + Csv("1. Historia") + "," + Csv(CAMPOS[i]) + "," + Csv("Respuesta del paciente") + "," + Csv(texto) + "," + Csv(fuente) + ",\"\",\"\"");
            }
            md.AppendLine();
        }

        md.AppendLine("## 2. Catálogo de intenciones (compartido por los 7 casos)");
        md.AppendLine();
        md.AppendLine("Los conjuntos son raíces sin tildes: dentro de un conjunto TODAS las raíces deben aparecer; basta que coincida UN conjunto. Las raíces de 5 o más letras toleran una letra de diferencia (errores de tipeo). Si el texto contiene alguna raíz **excluyente**, la intención no se aplica. Cuando coinciden varias intenciones gana la de conjunto más largo.");
        md.AppendLine();
        md.AppendLine("| Id | Descripción | Responde con | Conjuntos | Excluyentes | Validación (OK / Cambiar) | Comentario |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var it in cat.intenciones)
        {
            string conj = Formato(it.conjuntos), exc = it.excluyentes.Count == 0 ? "—" : Formato(it.excluyentes);
            md.AppendLine("| `" + it.id + "` | " + Esc(it.descripcion) + " | " + it.campoHistoria + " | " + Esc(conj) + " | " + Esc(exc) + " | | |");
            csv.AppendLine(Csv("(todos)") + "," + Csv("2. Catálogo") + "," + Csv(it.id) + "," + Csv(it.descripcion) + "," + Csv("Responde con: " + it.campoHistoria) + "," + Csv("Conjuntos: " + conj + " | Excluyentes: " + exc) + ",\"\",\"\"");
        }
        md.AppendLine();
        md.AppendLine("Respuesta cuando el campo es \"No refiere\": **" + cat.respuestaSinDato + "**");
        md.AppendLine();
        md.AppendLine("Respuesta cuando nada coincide (por caso, `respuestaGenericaSinMatch`): **" + FALLBACK + "**");
        md.AppendLine();
        csv.AppendLine(Csv("(todos)") + "," + Csv("2. Catálogo") + "," + Csv("respuestaSinDato") + "," + Csv("Cuando el campo es No refiere") + "," + Csv(cat.respuestaSinDato) + ",\"\",\"\",\"\"");
        csv.AppendLine(Csv("(todos)") + "," + Csv("2. Catálogo") + "," + Csv("respuestaGenericaSinMatch") + "," + Csv("Cuando nada coincide") + "," + Csv(FALLBACK) + ",\"\",\"\",\"\"");

        md.AppendLine("## 3. Mapeo intención → pregunta clave");
        md.AppendLine();
        md.AppendLine("**Aplicado** (equivalencia evidente por el texto de la pregunta): si el estudiante pregunta por la intención y el texto no coincide con las palabras de la pregunta clave, se registra esa pregunta (puntúa) y el paciente responde con su `respuestaPaciente` de siempre.");
        md.AppendLine();
        md.AppendLine("| Caso | Pregunta clave | Texto de la pregunta | Intención | Respuesta del paciente | Validación (OK / Cambiar) | Comentario |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var m in MAPEO_APLICADO)
        {
            var f = fichas.Find(x => x.idCaso == m[0]); var p = f.preguntasClave.Find(x => x.idPregunta == m[1]);
            md.AppendLine("| " + m[0] + " | `" + m[1] + "` | " + Esc(p.preguntaSugerida) + " | `" + m[2] + "` | " + Esc(p.respuestaPaciente) + " | | |");
            csv.AppendLine(Csv(m[0]) + "," + Csv("3. Mapeo aplicado") + "," + Csv(m[1]) + "," + Csv(p.preguntaSugerida) + "," + Csv("Intención: " + m[2]) + "," + Csv("Responde: " + p.respuestaPaciente) + ",\"\",\"\"");
        }
        md.AppendLine();
        md.AppendLine("**Propuestos y NO aplicados** (hay duda razonable; si el revisor los aprueba se agregan a `intencionesQueSatisface` de la pregunta):");
        md.AppendLine();
        md.AppendLine("| Caso | Pregunta clave | Intención | Por qué no se aplicó | Validación (OK / Cambiar) | Comentario |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (var m in MAPEO_PROPUESTO)
        {
            md.AppendLine("| " + m[0] + " | `" + m[1] + "` | `" + m[2] + "` | " + Esc(m[3]) + " | | |");
            csv.AppendLine(Csv(m[0]) + "," + Csv("3. Mapeo propuesto (no aplicado)") + "," + Csv(m[1]) + "," + Csv("Intención: " + m[2]) + "," + Csv(m[3]) + "," + Csv("") + ",\"\",\"\"");
        }
        md.AppendLine();

        md.AppendLine("## 4. Conjuntos agregados a las preguntas clave (cobertura de vocabulario)");
        md.AppendLine();
        md.AppendLine("\"dol\" no es subcadena de \"duele/duelen\", así que ¿\"Le duele el pecho al toser?\" no coincidía con su propia pregunta clave. A cada conjunto con \"dol\" se le agregó un gemelo con \"duel\" (se mantiene el original), más dos conjuntos para la pregunta de ortopnea del caso EPA. Solo AGREGAN formas de decir lo mismo: no cambia ninguna pregunta clave, su respuesta ni el puntaje máximo.");
        md.AppendLine();
        md.AppendLine("| Caso | Pregunta clave | Conjunto agregado | Validación (OK / Cambiar) | Comentario |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var f in fichas)
            foreach (var a in ConjuntosAgregados(f))
            {
                md.AppendLine("| " + a[0] + " | `" + a[1] + "` | {" + a[2] + "} | | |");
                csv.AppendLine(Csv(a[0]) + "," + Csv("4. Conjunto agregado") + "," + Csv(a[1]) + "," + Csv("Conjunto de palabras clave agregado") + "," + Csv("{" + a[2] + "}") + "," + Csv("") + ",\"\",\"\"");
            }
        md.AppendLine();
        md.AppendLine("## 5. Respuestas ya existentes que se modificaron (control anti-fuga)");
        md.AppendLine();
        md.AppendLine("El control anti-fuga encontró estas respuestas de preguntas clave que nombraban el diagnóstico del caso. Se reescribieron sin la palabra, conservando lo que el paciente sabe. **Requieren revisión clínica**: la versión nueva es menos específica.");
        md.AppendLine();
        md.AppendLine("| Caso | Pregunta clave | Antes | Ahora | Validación (OK / Cambiar) | Comentario |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach (var c in CORRECCIONES_FUGA)
        {
            md.AppendLine("| " + c[0] + " | `" + c[1] + "` | " + Esc(c[2]) + " | " + Esc(c[3]) + " | | |");
            csv.AppendLine(Csv(c[0]) + "," + Csv("5. Respuesta modificada") + "," + Csv(c[1]) + "," + Csv("Antes: " + c[2]) + "," + Csv("Ahora: " + c[3]) + "," + Csv("Control anti-fuga: nombraba el diagnóstico") + ",\"\",\"\"");
        }
        md.AppendLine();
        md.AppendLine("## 6. Observaciones para el revisor");
        md.AppendLine();
        string informe = EjecutarControl(out int hallazgos);
        md.AppendLine("- **Control anti-fuga** (palabras del propio diagnóstico en historia, respuestas y catálogo): " + hallazgos + " coincidencia(s). Detalle en `docs/antifuga_historia_paciente.txt`.");
        foreach (var linea in informe.Split('\n'))
            if (linea.TrimStart().StartsWith("COINCIDE"))
                md.AppendLine("  - " + linea.Trim());
        md.AppendLine("- Las únicas respuestas existentes que se modificaron son las de la sección 5 (las que nombraban el diagnóstico).");
        md.AppendLine("- Concordancia de género en respuestas ya existentes: varias respuestas usan el masculino con pacientes de sexo femenino (p. ej. BQ \"afiebrado\"/\"resfriado\" con una paciente, EPA \"sentado\"/\"acostado\" con una paciente). La historia nueva evita la marca de género o usa el género de la ficha.");
        md.AppendLine("- Preguntas sin reconocer: con `registrarSinReconocer` activo, el GestorCasoClinico anota cada pregunta no reconocida en `anamnesis_sin_reconocer.csv` (Application.persistentDataPath) para ampliar el vocabulario.");
        File.WriteAllText(Path.Combine(docs, "validacion_historia_paciente.md"), md.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(docs, "validacion_historia_paciente.csv"), csv.ToString(), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(docs, "antifuga_historia_paciente.txt"), informe, new UTF8Encoding(false));
        Debug.Log("[Anamnesis] documentos generados en docs/: historia_paciente_pendientes.md, validacion_historia_paciente.md/.csv, antifuga_historia_paciente.txt");
    }

    static string Formato(List<ConjuntoPalabrasClave> conjuntos)
    {
        var partes = new List<string>();
        foreach (var c in conjuntos) partes.Add("{" + string.Join(",", c.palabras.ToArray()) + "}");
        return string.Join(" ", partes.ToArray());
    }

    static string Esc(string s) => (s ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
}
#endif
