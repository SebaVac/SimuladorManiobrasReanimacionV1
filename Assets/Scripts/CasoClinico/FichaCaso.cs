using System.Collections.Generic;
using UnityEngine;

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
/// ayuda que muestra la UI, no la lógica de evaluación.</summary>
public enum SubmodoCaso
{
    Tutorial,    // explica las fases y guía paso a paso
    Guiado,      // pistas contextuales sin revelar aciertos
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

[System.Serializable]
public class ExamenComplementario
{
    public string nombreExamen;
    [TextArea] public string resultado;
    public bool esExamenPertinente;
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
}

[System.Serializable]
public class AccionCritica
{
    [TextArea] public string descripcion;
    public TipoAccion tipo;
    [Tooltip("Vincula con idPregunta / regionAnatomica / nombreExamen / id de conducta, según 'tipo'.")]
    public string idReferencia;
    public int peso;
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
    [TextArea] public string respuestaGenericaSinMatch = "No estoy segura de a qué se refiere con eso.";

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
