using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// ── MÓDULO DE RAZONAMIENTO CLÍNICO BASADO EN CASOS ────────────────────────────
// Datos puros del caso clínico. Sin lógica, sin MonoBehaviour.
//
// Este archivo reemplaza el diseño anterior de "evolución fisiológica por fases"
// (FichaCaso/FaseClinica/TriggerTransicion) por el modelo de razonamiento clínico:
// anamnesis + examen físico + exámenes complementarios + diagnóstico, con
// evaluación silenciosa y debriefing final.
//
// Todos los tipos son [System.Serializable] para poder editarse en el Inspector
// como parte del ScriptableObject FichaCaso.

// ── ENUMERACIONES ────────────────────────────────────────────────────────────

/// <summary>Categoría de una acción crítica; define contra qué registro del
/// jugador se evalúa su cumplimiento (ver MotorEvaluacion.EvaluarSiAccionFueCumplida).</summary>
public enum TipoAccion
{
    Anamnesis,             // se cumple si el jugador realizó la pregunta idReferencia
    ExamenFisico,          // se cumple si el jugador examinó la región idReferencia
    ExamenComplementario,  // se cumple si el jugador solicitó el examen idReferencia
    Diagnostico,           // reservado — evaluación del diagnóstico registrado
    Manejo                 // se cumple si el jugador marcó la conducta idReferencia
}

/// <summary>Submodo pedagógico con el que se juega un caso. Afecta el nivel de
/// ayuda que muestra la UI, no la lógica de evaluación.
/// "Guiado" existió y se eliminó (2026-09-22): no tenía ningún efecto de
/// comportamiento propio en ningún panel/gestor — era indistinguible de
/// Evaluacion en todo el código, mantenerlo era complejidad sin función.</summary>
public enum SubmodoCaso
{
    Tutorial,    // explica las fases y guía paso a paso
    Evaluacion   // sin ayudas; evaluación pura
}

// ── SUB-ESTRUCTURAS DE DATOS ─────────────────────────────────────────────────

[System.Serializable]
public class SignosVitales
{
    public int   frecuenciaCardiaca;     // lpm
    public int   frecuenciaRespiratoria; // rpm
    public string presionArterial;       // formato "120/80"
    public float saturacionO2;           // %
    public float temperatura;            // °C
}

[System.Serializable]
public class HallazgoRegion
{
    [Tooltip("Debe coincidir con el nombre de un Transform en InteraccionPaciente.regionesAnatomicas.")]
    public string regionAnatomica;
    [TextArea] public string hallazgoTexto;
    public bool esHallazgoRelevante;
}

/// <summary>Cómo puntúa pedir un examen en un caso. Los VALORES NUMÉRICOS coinciden a
/// propósito con el antiguo bool <c>esExamenPertinente</c> (0 = no, 1 = sí): los 7
/// .asset existentes se leen igual vía FormerlySerializedAs, sin reescribir datos.</summary>
public enum ClasificacionExamen
{
    NoPertinente = 0,  // pedirlo RESTA (penalización por examen no pertinente)
    Pertinente   = 1,  // pedirlo SUMA (entra en el denominador del componente Exámenes)
    Indiferente  = 2   // ni suma ni resta: no cuenta en el denominador ni en la penalización
}

[System.Serializable]
public class ExamenComplementario
{
    [Tooltip("Debe coincidir con el nombre de una entrada de CatalogoExamenes (se usa como id " +
             "en MotorEvaluacion y en los errores frecuentes 'omitio:<nombre>').")]
    public string nombreExamen;
    [TextArea] public string resultado;
    [FormerlySerializedAs("esExamenPertinente")]
    public ClasificacionExamen clasificacion = ClasificacionExamen.Pertinente;

    public bool EsPertinente => clasificacion == ClasificacionExamen.Pertinente;
}

/// <summary>Un conjunto AND de palabras: matchea solo si el texto del jugador
/// contiene TODAS ellas (en cualquier orden). Se usa como sustituto serializable
/// de <c>List&lt;List&lt;string&gt;&gt;</c> (Unity no serializa listas anidadas).</summary>
[System.Serializable]
public class ConjuntoPalabrasClave
{
    [Tooltip("TODAS estas palabras deben aparecer en el texto del jugador (AND). " +
             "Basta que UN conjunto de la lista matchee (OR entre conjuntos).")]
    public List<string> palabras = new List<string>();
}

[System.Serializable]
public class PreguntaAnamnesis
{
    public string idPregunta;
    public string preguntaSugerida;

    [Tooltip("Matching por texto libre: lista de conjuntos AND. La pregunta se " +
             "considera realizada si el texto del jugador contiene TODAS las palabras " +
             "de al menos UN conjunto. Reemplaza el matching por keyword suelta.")]
    public List<ConjuntoPalabrasClave> conjuntosPalabrasClave = new List<ConjuntoPalabrasClave>();

    [Tooltip("LEGADO — matching por keyword suelta (una palabra basta). Superado por " +
             "conjuntosPalabrasClave; se mantiene para compatibilidad de datos viejos.")]
    public List<string> palabrasClave = new List<string>();
    public List<string> sinonimos = new List<string>();

    [TextArea] public string respuestaPaciente;
    public bool esPreguntaClave;

    [Tooltip("Ids de intenciones del CatalogoIntenciones que esta pregunta clave satisface: si el texto del jugador no " +
             "coincide con los conjuntos de arriba pero sí con una de estas intenciones, se registra ESTA pregunta (puntúa) " +
             "y se responde con su respuestaPaciente. Solo cuando la equivalencia es evidente por el texto de la pregunta.")]
    public List<string> intencionesQueSatisface = new List<string>();
}

/// <summary>Campo de la <see cref="HistoriaPaciente"/> con el que responde una intención del catálogo.</summary>
public enum CampoHistoria
{
    Inicio, Evolucion, Empeora, Alivia, Remedios, Alergias, Antecedentes, Habitos,
    Intensidad, Localizacion, SintomasAsociados, Perfil,
    /// <summary>No es un campo de la historia: responde con <see cref="FichaCaso.motivoConsulta"/> (ya está en primera persona).</summary>
    MotivoConsulta
}

/// <summary>Historia del paciente en primera persona y tono hablado: es la ÚNICA fuente de lo que el paciente
/// responde a intenciones del catálogo que no corresponden a una pregunta clave. Se escribe SOLO con datos ya presentes
/// en la ficha (motivo de consulta, antecedentes, respuestas, datos administrativos); todo campo sin fuente queda en
/// "No refiere" hasta que lo valide un profesional (docs/historia_paciente_pendientes.md). Ninguna respuesta debe nombrar
/// el diagnóstico (control: Tools/Razonamiento Clínico/Anamnesis/Control anti-fuga).</summary>
[System.Serializable]
public class HistoriaPaciente
{
    public const string NO_REFIERE = "No refiere";

    [TextArea] public string inicio = NO_REFIERE;
    [TextArea] public string evolucion = NO_REFIERE;
    [TextArea] public string empeora = NO_REFIERE;
    [TextArea] public string alivia = NO_REFIERE;
    [TextArea] public string remedios = NO_REFIERE;
    [TextArea] public string alergias = NO_REFIERE;
    [TextArea] public string antecedentes = NO_REFIERE;
    [TextArea] public string habitos = NO_REFIERE;
    [Tooltip("Campos añadidos a la lista pedida porque el catálogo tiene las intenciones intensidad, localización y síntomas asociados.")]
    [TextArea] public string intensidad = NO_REFIERE;
    [TextArea] public string localizacion = NO_REFIERE;
    [TextArea] public string sintomasAsociados = NO_REFIERE;
    [Tooltip("Breve, solo para uso futuro: NO se usa en el matching ni lo contesta ninguna intención del catálogo.")]
    [TextArea] public string perfil = NO_REFIERE;

    /// <summary>Texto del campo, o null si no aplica (MotivoConsulta lo resuelve el llamador con la ficha).</summary>
    public string Obtener(CampoHistoria campo)
    {
        switch (campo)
        {
            case CampoHistoria.Inicio:            return inicio;
            case CampoHistoria.Evolucion:         return evolucion;
            case CampoHistoria.Empeora:           return empeora;
            case CampoHistoria.Alivia:            return alivia;
            case CampoHistoria.Remedios:          return remedios;
            case CampoHistoria.Alergias:          return alergias;
            case CampoHistoria.Antecedentes:      return antecedentes;
            case CampoHistoria.Habitos:           return habitos;
            case CampoHistoria.Intensidad:        return intensidad;
            case CampoHistoria.Localizacion:      return localizacion;
            case CampoHistoria.SintomasAsociados: return sintomasAsociados;
            case CampoHistoria.Perfil:            return perfil;
            default:                              return null;
        }
    }

    public static bool EsNoRefiere(string texto) =>
        string.IsNullOrWhiteSpace(texto) || string.Equals(texto.Trim(), NO_REFIERE, System.StringComparison.OrdinalIgnoreCase);
}

[System.Serializable]
public class AccionCritica
{
    [TextArea] public string descripcion;
    public TipoAccion tipo;
    [Tooltip("Vincula con idPregunta / regionAnatomica / nombreExamen / id de conducta, según 'tipo'.")]
    public string idReferencia;
    public int peso;

    [Tooltip("Solo para acciones de tipo Manejo: la acción se cumple si el texto de manejo " +
             "que escribió el jugador contiene TODAS las palabras de al menos UN conjunto " +
             "(AND dentro del conjunto, OR entre conjuntos; misma semántica que en preguntas " +
             "y diagnóstico). Sin conjuntos, la acción nunca se cumple (y se loguea un aviso).")]
    public List<ConjuntoPalabrasClave> conjuntosPalabrasClave = new List<ConjuntoPalabrasClave>();
}

[System.Serializable]
public class ErrorFrecuente
{
    public string descripcion;
    [Tooltip("Condición que dispara el error. Convención provisional: 'prefijo:valor' " +
             "con prefijo ∈ {omitio, solicito, examino, pregunto}. Ver MotorEvaluacion.SeDetectaError.")]
    public string idReferenciaCondicion;
    [TextArea] public string retroalimentacion;
}

/// <summary>Datos administrativos fijos del caso (encabezado de la ficha de papel
/// del Panel de Registro de Diagnóstico). NO son datos clínicos ni entran en la
/// evaluación: solo dan contexto visual de "paciente real". La fecha/hora de
/// ingreso NO va aquí — se registra dinámicamente al iniciar el caso
/// (ver GestorCasoClinico.FechaHoraInicio).</summary>
[System.Serializable]
public class DatosAdministrativos
{
    public string nombrePacienteFicticio;
    public string sexoPaciente;
    public string edadPaciente;
    public string nombreEstablecimiento;
}

// ── SCRIPTABLE OBJECT ────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "NuevoCaso", menuName = "RazonamientoClinico/Ficha de Caso")]
public class FichaCaso : ScriptableObject
{
    [Header("Identificación")]
    public string idCaso;
    public string nombreCaso;
    [TextArea] public string motivoConsulta;

    [Header("Antecedentes")]
    public List<string> antecedentesRelevantes = new List<string>();

    [Header("Signos Vitales")]
    public SignosVitales signosVitales = new SignosVitales();

    [Header("Examen Físico")]
    public List<HallazgoRegion> hallazgosPorRegion = new List<HallazgoRegion>();

    [Header("Exámenes Complementarios")]
    public List<ExamenComplementario> examenesDisponibles = new List<ExamenComplementario>();

    [Header("Anamnesis")]
    public List<PreguntaAnamnesis> preguntasClave = new List<PreguntaAnamnesis>();

    [Tooltip("Respuesta genérica del paciente cuando el texto libre no matchea ninguna " +
             "pregunta. No penaliza ni marca nada. Editable por caso, no obligatorio.")]
    [TextArea] public string respuestaGenericaSinMatch = "Disculpe, no le entendí bien. ¿Me lo puede preguntar de otra forma?";

    [Header("Historia del paciente (respuestas a intenciones del catálogo)")]
    public HistoriaPaciente historia = new HistoriaPaciente();

    [Header("Diagnóstico y Manejo")]
    [Tooltip("Referencia legible / fallback. Si conjuntosPalabrasClaveDiagnostico está " +
             "vacío, el diagnóstico se evalúa por comparación exacta contra este texto.")]
    public string diagnosticoEsperado;

    [Tooltip("Matching del diagnóstico por texto libre: conjuntos AND (misma semántica " +
             "que conjuntosPalabrasClave de anamnesis). Si está vacío, se usa " +
             "diagnosticoEsperado por comparación exacta.")]
    public List<ConjuntoPalabrasClave> conjuntosPalabrasClaveDiagnostico = new List<ConjuntoPalabrasClave>();

    public List<AccionCritica> accionesCriticas = new List<AccionCritica>();
    public List<ErrorFrecuente> erroresFrecuentes = new List<ErrorFrecuente>();

    [Header("Datos administrativos (ficha de papel — Panel de Registro de Diagnóstico)")]
    [Tooltip("Encabezado de la ficha clínica de papel. No entra en la evaluación.")]
    public DatosAdministrativos datosAdministrativos = new DatosAdministrativos();
}
