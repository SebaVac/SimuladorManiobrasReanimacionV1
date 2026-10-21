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
    [Header("Puntaje (modo Evaluación)")]
    [Tooltip("Pesos y parámetros del modelo de puntaje. Único lugar donde viven esos números.")]
    [SerializeField] private ConfigPuntaje config;

    private FichaCaso fichaActiva;

    // Respaldo si el asset no está cableado: instancia con los valores por defecto
    // de ConfigPuntaje (que SON el modelo vigente). Se loguea el error una sola vez.
    private ConfigPuntaje _configRespaldo;
    private ConfigPuntaje Config
    {
        get
        {
            if (config != null) return config;
            if (_configRespaldo == null)
            {
                Debug.LogError("[MotorEvaluacion] Falta asignar ConfigPuntaje en el Inspector; " +
                               "se usan los valores por defecto del modelo.", this);
                _configRespaldo = ScriptableObject.CreateInstance<ConfigPuntaje>();
            }
            return _configRespaldo;
        }
    }

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

    /// <summary>
    /// Evaluación final. Único punto que produce resultado visible.
    /// <paramref name="manejoTexto"/> es el texto libre de "Indicaciones / manejo" que
    /// escribió el jugador (puede ser null o vacío: el componente Manejo suma 0).
    /// <paramref name="submodo"/> solo se anota en el resultado (Tutorial no muestra
    /// puntaje total); el motor no ramifica su cálculo por submodo.
    /// </summary>
    public ResultadoCaso EvaluarCasoCompleto(string diagnosticoJugador, string manejoTexto, SubmodoCaso submodo)
    {
        var resultado = new ResultadoCaso
        {
            accionesCumplidas = new List<AccionCritica>(),
            accionesOmitidas  = new List<AccionCritica>(),
            erroresDetectados = new List<ErrorFrecuente>(),
            submodo           = submodo,
            diagnostico       = new ComponentePuntaje(),
            manejo            = new ComponentePuntaje(),
            examenes          = new ComponentePuntaje(),
            anamnesis         = new ComponentePuntaje(),
            examenFisico      = new ComponentePuntaje()
        };

        if (fichaActiva == null)
        {
            Debug.LogError("[MotorEvaluacion] EvaluarCasoCompleto sin ficha activa.", this);
            return resultado;
        }

        var cfg = Config;
        string manejo = manejoTexto ?? string.Empty;

        // ── Acciones críticas (listas del debriefing) + base del componente Manejo ──
        float pesoManejoTotal = 0f, pesoManejoCumplido = 0f;
        foreach (var accion in fichaActiva.accionesCriticas)
        {
            if (accion == null) continue;

            bool esManejo = accion.tipo == TipoAccion.Manejo;
            if (esManejo && (accion.conjuntosPalabrasClave == null || accion.conjuntosPalabrasClave.Count == 0))
                Debug.LogWarning($"[MotorEvaluacion] La acción de Manejo '{accion.idReferencia}' del caso " +
                                 $"'{fichaActiva.idCaso}' no tiene conjuntosPalabrasClave: no puede cumplirse.", this);

            bool cumplida = EvaluarSiAccionFueCumplida(accion, manejo);
            if (cumplida) resultado.accionesCumplidas.Add(accion);
            else          resultado.accionesOmitidas.Add(accion);

            if (esManejo)
            {
                pesoManejoTotal += accion.peso;
                if (cumplida) pesoManejoCumplido += accion.peso;
            }
        }

        foreach (var error in fichaActiva.erroresFrecuentes)
        {
            if (SeDetectaError(error, manejo)) resultado.erroresDetectados.Add(error);
        }

        // ── Diagnóstico: todo o nada ──────────────────────────────────────────
        var conjuntosDx = fichaActiva.conjuntosPalabrasClaveDiagnostico;
        resultado.diagnosticoCorrecto = (conjuntosDx != null && conjuntosDx.Count > 0)
            ? ContienePalabraClave(diagnosticoJugador, conjuntosDx)
            : NormalizarTexto(diagnosticoJugador) == NormalizarTexto(fichaActiva.diagnosticoEsperado);

        resultado.diagnostico.maximo   = cfg.pesoDiagnostico;
        resultado.diagnostico.obtenido = resultado.diagnosticoCorrecto ? cfg.pesoDiagnostico : 0f;

        // ── Manejo: proporcional al peso de las acciones de Manejo cumplidas ──────
        if (pesoManejoTotal > 0f)
        {
            resultado.manejo.maximo   = cfg.pesoManejo;
            resultado.manejo.obtenido = cfg.pesoManejo * (pesoManejoCumplido / pesoManejoTotal);
        }

        // ── Exámenes: pertinentes pedidos − penalización por no pertinentes ───────
        // Tres estados (ClasificacionExamen): Pertinente suma y entra en el denominador;
        // Indiferente no cuenta ni en el denominador ni en la penalización; NoPertinente
        // resta si se pidió. Además, todo examen PEDIDO que la ficha no trae (el resto del
        // catálogo ampliado en modo Evaluación) es NoPertinente por defecto.
        int pertinentesTotales = 0, pertinentesPedidos = 0, noPertinentesPedidos = 0;
        var nombresDeLaFicha = new HashSet<string>();
        foreach (var examen in fichaActiva.examenesDisponibles)
        {
            if (examen == null) continue;
            nombresDeLaFicha.Add(examen.nombreExamen);
            bool pedido = examenesSolicitados.Contains(examen.nombreExamen);
            switch (examen.clasificacion)
            {
                case ClasificacionExamen.Pertinente:
                    pertinentesTotales++;
                    if (pedido) pertinentesPedidos++;
                    break;
                case ClasificacionExamen.NoPertinente:
                    if (pedido) noPertinentesPedidos++;
                    break;
                // Indiferente: no hace nada a propósito.
            }
        }
        foreach (var pedidoId in examenesSolicitados)
            if (!nombresDeLaFicha.Contains(pedidoId)) noPertinentesPedidos++;
        if (pertinentesTotales > 0)
        {
            float valorPorExamen = cfg.pesoExamenes / pertinentesTotales;
            float bruto = valorPorExamen * pertinentesPedidos
                        - cfg.factorPenalizacionExamenNoPertinente * valorPorExamen * noPertinentesPedidos;
            resultado.examenes.maximo   = cfg.pesoExamenes;
            resultado.examenes.obtenido = Mathf.Max(0f, bruto);
        }

        // ── Anamnesis: preguntas clave reconocidas (distintas) ────────────────────
        int clavesTotales = 0, clavesRealizadas = 0;
        foreach (var pregunta in fichaActiva.preguntasClave)
        {
            if (pregunta == null || !pregunta.esPreguntaClave) continue;
            clavesTotales++;
            if (preguntasRealizadas.Contains(pregunta.idPregunta)) clavesRealizadas++;
        }
        if (clavesTotales > 0)
        {
            resultado.anamnesis.maximo   = cfg.pesoAnamnesis;
            resultado.anamnesis.obtenido = cfg.pesoAnamnesis * clavesRealizadas / clavesTotales;
        }

        // ── Examen físico: regiones relevantes examinadas (nombres distintos) ─────
        var regionesRelevantes = new HashSet<string>();
        foreach (var hallazgo in fichaActiva.hallazgosPorRegion)
            if (hallazgo != null && hallazgo.esHallazgoRelevante && !string.IsNullOrEmpty(hallazgo.regionAnatomica))
                regionesRelevantes.Add(hallazgo.regionAnatomica);
        if (regionesRelevantes.Count > 0)
        {
            int examinadas = 0;
            foreach (var region in regionesRelevantes)
                if (regionesExaminadas.Contains(region)) examinadas++;
            resultado.examenFisico.maximo   = cfg.pesoExamenFisico;
            resultado.examenFisico.obtenido = cfg.pesoExamenFisico * examinadas / regionesRelevantes.Count;
        }

        // ── Errores frecuentes: resta con tope ────────────────────────────────────
        resultado.penalizacionErrores = Mathf.Min(
            resultado.erroresDetectados.Count * cfg.penalizacionPorError,
            cfg.topePenalizacionErrores);

        // ── Total ─────────────────────────────────────────────────────────────────
        float maximoTotal = resultado.diagnostico.maximo + resultado.manejo.maximo + resultado.examenes.maximo
                          + resultado.anamnesis.maximo + resultado.examenFisico.maximo;
        float bruto2 = resultado.diagnostico.obtenido + resultado.manejo.obtenido + resultado.examenes.obtenido
                     + resultado.anamnesis.obtenido + resultado.examenFisico.obtenido
                     - resultado.penalizacionErrores;
        resultado.totalExacto   = Mathf.Clamp(bruto2, 0f, maximoTotal);
        resultado.puntajeMaximo = Mathf.RoundToInt(maximoTotal);
        resultado.puntajeTotal  = Mathf.RoundToInt(resultado.totalExacto);

        return resultado;
    }

    // ── Lógica interna ───────────────────────────────────────────────────────

    private bool EvaluarSiAccionFueCumplida(AccionCritica accion, string manejoTexto)
    {
        switch (accion.tipo)
        {
            case TipoAccion.Anamnesis:            return preguntasRealizadas.Contains(accion.idReferencia);
            case TipoAccion.ExamenFisico:         return regionesExaminadas.Contains(accion.idReferencia);
            case TipoAccion.ExamenComplementario: return examenesSolicitados.Contains(accion.idReferencia);
            // Manejo: texto libre contra los conjuntos de la acción. Sin conjuntos
            // (lista vacía/null) ContienePalabraClave devuelve false — nunca lanza.
            case TipoAccion.Manejo:               return ContienePalabraClave(manejoTexto, accion.conjuntosPalabrasClave);
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
    private bool SeDetectaError(ErrorFrecuente error, string manejoTexto)
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
                return !AccionCumplidaPorId(valor, manejoTexto);
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

    private bool AccionCumplidaPorId(string idAccion, string manejoTexto)
    {
        foreach (var accion in fichaActiva.accionesCriticas)
        {
            if (accion == null || accion.idReferencia != idAccion) continue;
            return EvaluarSiAccionFueCumplida(accion, manejoTexto);
        }
        // Si no es una AccionCritica, interpretamos idAccion como referencia directa
        // a un registro del jugador. (Ya no hay lista de ids de manejo: el manejo
        // es texto libre y solo puede evaluarse a través de una AccionCritica.)
        return preguntasRealizadas.Contains(idAccion)
            || regionesExaminadas.Contains(idAccion)
            || examenesSolicitados.Contains(idAccion);
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

/// <summary>Puntos obtenidos y máximos de un componente del modelo de puntaje.
/// <c>maximo</c> es 0 si el componente no aplica al caso (p. ej. un caso sin acciones
/// de Manejo): así el total máximo refleja solo lo que el caso permite ganar.</summary>
public class ComponentePuntaje
{
    public float obtenido;
    public float maximo;
}

/// <summary>Resultado final del caso. Se construye una sola vez y viaja en
/// GestorCasoClinico.OnCasoFinalizado hacia el panel de debriefing.</summary>
public class ResultadoCaso
{
    /// <summary>Total redondeado (con piso 0 y techo = puntajeMaximo). El Debriefing solo
    /// lo muestra en modo Evaluación — ver <see cref="EsEvaluacion"/>.</summary>
    public int puntajeTotal;
    public int puntajeMaximo;
    public List<AccionCritica> accionesCumplidas;
    public List<AccionCritica> accionesOmitidas;
    public List<ErrorFrecuente> erroresDetectados;
    public bool diagnosticoCorrecto;

    /// <summary>Submodo con el que se jugó el caso. Tutorial no puntúa: se muestra el
    /// desglose por componente sin nota total.</summary>
    public SubmodoCaso submodo;
    public bool EsEvaluacion => submodo == SubmodoCaso.Evaluacion;

    // Desglose por componente (obtenido/máximo).
    public ComponentePuntaje diagnostico;
    public ComponentePuntaje manejo;
    public ComponentePuntaje examenes;
    public ComponentePuntaje anamnesis;
    public ComponentePuntaje examenFisico;

    /// <summary>Puntos que restan los errores frecuentes detectados (valor positivo, ya con tope).</summary>
    public float penalizacionErrores;

    /// <summary>Total sin redondear.</summary>
    public float totalExacto;

    /// <summary>Dispositivo de entrada usado en el caso (Manos, Controles o Mixto). Solo se registra y se
    /// muestra en el Debriefing; no afecta al puntaje.</summary>
    public DispositivoEntrada dispositivoEntrada;
}
