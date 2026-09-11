using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Matching y scoring del caso clínico. Evaluación SILENCIOSA:
///   - Durante la partida solo acumula registros (qué examinó, qué preguntó,
///     qué exámenes pidió). Nunca devuelve ni expone aciertos/errores.
///   - EvaluarCasoCompleto() se llama una sola vez al cierre y produce el
///     ResultadoCaso que el debriefing muestra.
///
/// No conoce a InteraccionPaciente ni a la UI. El GestorCasoClinico le reenvía
/// los eventos del jugador.
///
/// Allocations: las normalizaciones de texto ocurren en acciones discretas del
/// jugador (no por frame). El cache de palabras clave se construye una sola vez
/// en Inicializar().
/// </summary>
public class MotorEvaluacion : MonoBehaviour
{
    private FichaCaso fichaActiva;

    private readonly HashSet<string> regionesExaminadas  = new HashSet<string>();
    private readonly HashSet<string> preguntasRealizadas = new HashSet<string>();
    private readonly HashSet<string> examenesSolicitados = new HashSet<string>();

    // Cache de palabras clave + sinónimos normalizados, por idPregunta.
    private readonly Dictionary<string, List<string>> palabrasClaveNormalizadasPorPregunta =
        new Dictionary<string, List<string>>();

    // StringBuilder reutilizable para NormalizarTexto (evita 1 alloc por normalización).
    private readonly StringBuilder _sbNorm = new StringBuilder(128);

    // ── API pública ──────────────────────────────────────────────────────────

    public void Inicializar(FichaCaso ficha)
    {
        fichaActiva = ficha;
        regionesExaminadas.Clear();
        preguntasRealizadas.Clear();
        examenesSolicitados.Clear();
        palabrasClaveNormalizadasPorPregunta.Clear();

        if (ficha == null)
        {
            Debug.LogError("[MotorEvaluacion] Inicializar recibió una ficha nula.", this);
            return;
        }

        foreach (var pregunta in ficha.preguntasClave)
        {
            var todasLasPalabras = new List<string>();
            if (pregunta.palabrasClave != null)
                foreach (var p in pregunta.palabrasClave) todasLasPalabras.Add(NormalizarTexto(p));
            if (pregunta.sinonimos != null)
                foreach (var s in pregunta.sinonimos) todasLasPalabras.Add(NormalizarTexto(s));
            palabrasClaveNormalizadasPorPregunta[pregunta.idPregunta] = todasLasPalabras;
        }
    }

    public void RegistrarExamenRegion(string idRegion)   => regionesExaminadas.Add(idRegion);
    public void RegistrarPreguntaAlternativa(string idPregunta) => preguntasRealizadas.Add(idPregunta);
    public void RegistrarExamenSolicitado(string idExamen) => examenesSolicitados.Add(idExamen);

    /// <summary>Cantidad de preguntas DISTINTAS reconocidas hasta ahora en el caso
    /// (idPregunta únicos: repetir la misma pregunta no suma, porque es un HashSet).
    /// Solo lectura, sin tocar la lógica de matching. El Panel de Anamnesis lo usa
    /// para su guard de "al menos 1 pregunta reconocida".</summary>
    public int PreguntasRealizadasCount => preguntasRealizadas.Count;

    /// <summary>Cantidad de exámenes DISTINTOS solicitados hasta ahora (nombreExamen
    /// únicos: volver a tocar el mismo examen no suma). Solo lectura. El Panel de
    /// Exámenes Complementarios lo usa para su guard de "al menos 1 examen".</summary>
    public int ExamenesSolicitadosCount => examenesSolicitados.Count;

    /// <summary>
    /// Matching por CONJUNTOS de palabras clave (AND dentro del conjunto, OR entre
    /// conjuntos). Normaliza <paramref name="textoJugador"/> y cada palabra
    /// (minúsculas, sin diacríticos) y verifica, para cada conjunto, que TODAS sus
    /// palabras aparezcan como substring del texto en cualquier orden. Devuelve
    /// true en el primer conjunto que cumpla.
    ///
    /// No corre en Update() — sin restricción de cero-allocation.
    /// </summary>
    public bool ContienePalabraClave(string textoJugador, List<ConjuntoPalabrasClave> conjuntos)
    {
        if (conjuntos == null || conjuntos.Count == 0 || string.IsNullOrWhiteSpace(textoJugador))
            return false;

        string texto = NormalizarTexto(textoJugador);

        foreach (var conjunto in conjuntos)
        {
            if (conjunto == null || conjunto.palabras == null || conjunto.palabras.Count == 0)
                continue;

            bool todasPresentes = true;
            bool algunaPalabraValida = false;

            foreach (var palabra in conjunto.palabras)
            {
                string p = NormalizarTexto(palabra);
                if (p.Length == 0) continue;      // palabra en blanco → se ignora
                algunaPalabraValida = true;
                if (!texto.Contains(p)) { todasPresentes = false; break; }
            }

            if (algunaPalabraValida && todasPresentes) return true;
        }

        return false;
    }

    /// <summary>Texto libre de anamnesis: si contiene alguna palabra clave/sinónimo
    /// de una pregunta, esa pregunta se cuenta como realizada.</summary>
    public void RegistrarTextoLibre(string texto)
    {
        if (fichaActiva == null || string.IsNullOrEmpty(texto)) return;

        string normalizado = NormalizarTexto(texto);
        foreach (var pregunta in fichaActiva.preguntasClave)
        {
            if (preguntasRealizadas.Contains(pregunta.idPregunta)) continue;
            if (!palabrasClaveNormalizadasPorPregunta.TryGetValue(pregunta.idPregunta, out var palabras))
                continue;

            for (int i = 0; i < palabras.Count; i++)
            {
                if (palabras[i].Length == 0) continue;
                if (normalizado.Contains(palabras[i]))
                {
                    preguntasRealizadas.Add(pregunta.idPregunta);
                    break;
                }
            }
        }
    }

    /// <summary>Evaluación final. Único punto que produce resultado visible.</summary>
    public ResultadoCaso EvaluarCasoCompleto(string diagnosticoJugador, List<string> manejoJugador)
    {
        var resultado = new ResultadoCaso
        {
            accionesCumplidas = new List<AccionCritica>(),
            accionesOmitidas  = new List<AccionCritica>(),
            erroresDetectados = new List<ErrorFrecuente>()
        };

        if (fichaActiva == null)
        {
            Debug.LogError("[MotorEvaluacion] EvaluarCasoCompleto sin ficha activa.", this);
            return resultado;
        }

        var manejo = manejoJugador ?? new List<string>();

        foreach (var accion in fichaActiva.accionesCriticas)
        {
            bool cumplida = EvaluarSiAccionFueCumplida(accion, manejo);
            if (cumplida)
            {
                resultado.accionesCumplidas.Add(accion);
                resultado.puntajeTotal += accion.peso;
            }
            else
            {
                resultado.accionesOmitidas.Add(accion);
            }
            resultado.puntajeMaximo += accion.peso;
        }

        foreach (var error in fichaActiva.erroresFrecuentes)
        {
            if (SeDetectaError(error, manejo)) resultado.erroresDetectados.Add(error);
        }

        var conjuntosDx = fichaActiva.conjuntosPalabrasClaveDiagnostico;
        resultado.diagnosticoCorrecto = (conjuntosDx != null && conjuntosDx.Count > 0)
            ? ContienePalabraClave(diagnosticoJugador, conjuntosDx)
            : NormalizarTexto(diagnosticoJugador) == NormalizarTexto(fichaActiva.diagnosticoEsperado);

        return resultado;
    }

    // ── Lógica interna ───────────────────────────────────────────────────────

    private bool EvaluarSiAccionFueCumplida(AccionCritica accion, List<string> manejoJugador)
    {
        switch (accion.tipo)
        {
            case TipoAccion.Anamnesis:            return preguntasRealizadas.Contains(accion.idReferencia);
            case TipoAccion.ExamenFisico:         return regionesExaminadas.Contains(accion.idReferencia);
            case TipoAccion.ExamenComplementario: return examenesSolicitados.Contains(accion.idReferencia);
            case TipoAccion.Manejo:               return manejoJugador.Contains(accion.idReferencia);
            case TipoAccion.Diagnostico:          return false; // se evalúa aparte (resultado.diagnosticoCorrecto)
            default:                              return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PENDIENTE DE VALIDACIÓN CON EL EQUIPO — implementación provisional.
    //
    // La convención de ErrorFrecuente.idReferenciaCondicion NO está cerrada.
    // Implementación base: cadena "prefijo:valor".
    //   omitio:<idAccion>   → el error se detecta si esa AccionCritica NO fue cumplida
    //   pregunto:<idPregunta> → se detecta si el jugador SÍ realizó esa pregunta
    //   examino:<region>    → se detecta si el jugador SÍ examinó esa región
    //   solicito:<idExamen> → se detecta si el jugador SÍ solicitó ese examen
    //                         (típicamente un examen no pertinente)
    // Sin prefijo reconocido → no se detecta (return false) y se loguea aviso.
    // ─────────────────────────────────────────────────────────────────────────
    private bool SeDetectaError(ErrorFrecuente error, List<string> manejoJugador)
    {
        string cond = error != null ? error.idReferenciaCondicion : null;
        if (string.IsNullOrEmpty(cond)) return false;

        int sep = cond.IndexOf(':');
        if (sep <= 0 || sep >= cond.Length - 1)
        {
            Debug.LogWarning($"[MotorEvaluacion] idReferenciaCondicion sin formato 'prefijo:valor': '{cond}'.", this);
            return false;
        }

        string prefijo = cond.Substring(0, sep).Trim().ToLowerInvariant();
        string valor   = cond.Substring(sep + 1).Trim();

        switch (prefijo)
        {
            case "omitio":
                return !AccionCumplidaPorId(valor, manejoJugador);
            case "pregunto":
                return preguntasRealizadas.Contains(valor);
            case "examino":
                return regionesExaminadas.Contains(valor);
            case "solicito":
                return examenesSolicitados.Contains(valor);
            default:
                Debug.LogWarning($"[MotorEvaluacion] Prefijo de condición no reconocido: '{prefijo}'.", this);
                return false;
        }
    }

    private bool AccionCumplidaPorId(string idAccion, List<string> manejoJugador)
    {
        foreach (var accion in fichaActiva.accionesCriticas)
        {
            if (accion.idReferencia != idAccion) continue;
            return EvaluarSiAccionFueCumplida(accion, manejoJugador);
        }
        // Si no es una AccionCritica, interpretamos idAccion como referencia directa.
        return preguntasRealizadas.Contains(idAccion)
            || regionesExaminadas.Contains(idAccion)
            || examenesSolicitados.Contains(idAccion)
            || manejoJugador.Contains(idAccion);
    }

    private string NormalizarTexto(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        string descompuesto = texto.Normalize(NormalizationForm.FormD);
        _sbNorm.Clear();
        for (int i = 0; i < descompuesto.Length; i++)
        {
            char c = descompuesto[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                _sbNorm.Append(c);
        }
        return _sbNorm.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }
}

/// <summary>Resultado final del caso. Se construye una sola vez y viaja en
/// GestorCasoClinico.OnCasoFinalizado hacia el panel de debriefing.</summary>
public class ResultadoCaso
{
    public int puntajeTotal;
    public int puntajeMaximo;
    public List<AccionCritica> accionesCumplidas;
    public List<AccionCritica> accionesOmitidas;
    public List<ErrorFrecuente> erroresDetectados;
    public bool diagnosticoCorrecto;
}
