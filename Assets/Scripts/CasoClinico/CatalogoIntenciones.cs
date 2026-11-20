using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una forma de preguntar por algo de la historia del paciente ("¿hace cuánto le duele?", "¿qué remedios toma?").
/// Los conjuntos reutilizan <see cref="ConjuntoPalabrasClave"/> (AND dentro del conjunto, OR entre conjuntos) y se escriben
/// como raíces sin tildes y en minúscula ("duel", "dol", "molest", "cuant"...).
/// </summary>
[System.Serializable]
public class IntencionConsulta
{
    [Tooltip("Identificador estable (lo referencian PreguntaAnamnesis.intencionesQueSatisface y los documentos de validación).")]
    public string id;
    [TextArea] public string descripcion;
    [Tooltip("Formas cotidianas de preguntarlo: AND dentro del conjunto, OR entre conjuntos.")]
    public List<ConjuntoPalabrasClave> conjuntos = new List<ConjuntoPalabrasClave>();
    [Tooltip("Si el texto contiene alguno de estos conjuntos la intención NO se aplica (p. ej. '¿hace cuánto fuma?' no es " +
             "'cuánto tiempo lleva el síntoma'). Las exclusiones se evalúan SIN tolerancia de tipeo.")]
    public List<ConjuntoPalabrasClave> excluyentes = new List<ConjuntoPalabrasClave>();
    [Tooltip("Con qué campo de la historia del paciente responde.")]
    public CampoHistoria campoHistoria;
    [Tooltip("Intención genérica: solo se evalúa si NINGUNA otra intención coincide (p. ej. \"¿qué le pasa?\").")]
    public bool ultimoRecurso;
}

/// <summary>
/// Catálogo compartido (no por caso) de intenciones de anamnesis. Es el segundo paso de la resolución de una pregunta
/// escrita (GestorCasoClinico.OnJugadorEscribioPregunta): si no coincide ninguna pregunta clave del caso, se busca la
/// intención más específica del catálogo. Determinista, sin IA ni red. El puntaje NO depende de este catálogo: solo
/// puntúa una pregunta clave cuando la intención está vinculada a ella (PreguntaAnamnesis.intencionesQueSatisface).
/// </summary>
[CreateAssetMenu(fileName = "CatalogoIntenciones", menuName = "RazonamientoClinico/Catálogo de intenciones")]
public class CatalogoIntenciones : ScriptableObject
{
    public List<IntencionConsulta> intenciones = new List<IntencionConsulta>();

    [Tooltip("Lo que dice el paciente cuando la historia del caso no tiene dato para el campo (\"No refiere\"). No afirma ni niega nada.")]
    [TextArea] public string respuestaSinDato = "Mmm, no sabría decirle.";

    /// <summary>La intención que mejor coincide con el texto: la de conjunto más largo (más palabras que coinciden juntas);
    /// en empate, la primera del catálogo. null si ninguna.</summary>
    public IntencionConsulta Resolver(string texto, MotorEvaluacion motor)
    {
        if (motor == null || string.IsNullOrWhiteSpace(texto)) return null;

        var mejor = MejorIntencion(texto, motor, false);
        return mejor != null ? mejor : MejorIntencion(texto, motor, true);
    }

    private IntencionConsulta MejorIntencion(string texto, MotorEvaluacion motor, bool genericas)
    {
        IntencionConsulta mejor = null;
        int mejorPuntaje = 0;
        for (int i = 0; i < intenciones.Count; i++)
        {
            var it = intenciones[i];
            if (it == null || it.ultimoRecurso != genericas) continue;
            int puntaje = motor.CoincidenciaConjuntos(texto, it.conjuntos, true);
            if (puntaje <= mejorPuntaje) continue;
            if (it.excluyentes != null && it.excluyentes.Count > 0 && motor.ContienePalabraClave(texto, it.excluyentes, false)) continue;
            mejor = it;
            mejorPuntaje = puntaje;
        }
        return mejor;
    }

    public IntencionConsulta Buscar(string id)
    {
        for (int i = 0; i < intenciones.Count; i++)
            if (intenciones[i] != null && intenciones[i].id == id) return intenciones[i];
        return null;
    }

    /// <summary>Respuesta del paciente para la intención, tomada SOLO de la historia (o del motivo de consulta). Si el campo
    /// no tiene dato, <see cref="respuestaSinDato"/>.</summary>
    public string Responder(IntencionConsulta intencion, FichaCaso ficha)
    {
        string texto = intencion.campoHistoria == CampoHistoria.MotivoConsulta
            ? ficha.motivoConsulta
            : (ficha.historia != null ? ficha.historia.Obtener(intencion.campoHistoria) : null);
        return HistoriaPaciente.EsNoRefiere(texto) ? respuestaSinDato : texto;
    }
}
