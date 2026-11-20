using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Orquestador del módulo de Razonamiento Clínico. Máquina de estados del caso.
///
/// Desacoplamiento estricto:
///   - Es el ÚNICO punto que conoce a InteraccionPaciente y MotorEvaluacion.
///   - InteraccionPaciente y MotorEvaluacion NO se referencian entre sí:
///     toda comunicación pasa por este Gestor vía eventos C#.
///
/// Evaluación invisible:
///   - Ninguna interacción durante la partida revela correcto/incorrecto.
///   - El MotorEvaluacion acumula en silencio; el resultado se expone una sola
///     vez en OnCasoFinalizado, ya en fase Debriefing.
/// </summary>
public class GestorCasoClinico : MonoBehaviour
{
    [Header("Configuración del caso")]
    [SerializeField] private FichaCaso casoActual;
    [SerializeField] private SubmodoCaso submodoActual = SubmodoCaso.Tutorial;

    [Header("Dispositivo de entrada (opcional)")]
    [Tooltip("Si se asigna, el dispositivo usado en el caso (Manos / Controles / Mixto) se anota en ResultadoCaso " +
             "y aparece en el Debriefing. No afecta al puntaje.")]
    [SerializeField] private EntradaControlesTouch entradaDispositivo;

    [Header("Anamnesis por texto libre")]
    [Tooltip("Catálogo compartido de intenciones (segundo paso de la resolución de una pregunta escrita). Opcional: sin él " +
             "solo se reconocen las preguntas clave del caso.")]
    [SerializeField] private CatalogoIntenciones catalogoIntenciones;
    [Tooltip("Guarda las preguntas que no se reconocieron: línea [ANAMNESIS] en el log (la primera vez de cada texto) y " +
             "anamnesis_sin_reconocer.csv en Application.persistentDataPath (fecha, idCaso, submodo, texto) para ampliar el vocabulario.")]
    [SerializeField] private bool registrarSinReconocer = true;

    [Header("Subsistemas (asignar en Inspector)")]
    [SerializeField] private InteraccionPaciente interaccionPaciente;
    [SerializeField] private MotorEvaluacion motorEvaluacion;

    public EstadoCasoClinico EstadoActual { get; private set; } = EstadoCasoClinico.Inactivo;
    public FichaCaso CasoActual   => casoActual;
    public SubmodoCaso SubmodoActual => submodoActual;

    /// <summary>Preguntas DISTINTAS reconocidas en el caso en curso (passthrough de
    /// <see cref="MotorEvaluacion.PreguntasRealizadasCount"/>; el panel solo habla
    /// con el Gestor, no con el Motor). El Panel de Anamnesis lo usa para habilitar
    /// "Continuar" solo tras ≥1 pregunta reconocida.</summary>
    public int PreguntasReconocidasCount => motorEvaluacion != null ? motorEvaluacion.PreguntasRealizadasCount : 0;

    /// <summary>Intercambios de anamnesis DISTINTOS reconocidos: preguntas clave (las mismas de
    /// <see cref="PreguntasReconocidasCount"/>) más las intenciones del catálogo que NO puntúan (sin pregunta clave vinculada).
    /// Habilita "Continuar" en el Panel de Anamnesis: una pregunta válida que no puntúa no deja el botón bloqueado.
    /// El puntaje sigue dependiendo SOLO de <see cref="PreguntasReconocidasCount"/>.</summary>
    public int IntercambiosReconocidosCount => PreguntasReconocidasCount + _intencionesLibres.Count;

    private readonly HashSet<string> _intencionesLibres = new HashSet<string>();
    private readonly HashSet<string> _sinReconocerYaLogueadas = new HashSet<string>();
    private readonly StringBuilder _sbCsv = new StringBuilder(256);

    /// <summary>Exámenes DISTINTOS solicitados en el caso en curso (passthrough de
    /// <see cref="MotorEvaluacion.ExamenesSolicitadosCount"/>). El Panel de Exámenes
    /// Complementarios lo usa para habilitar "Continuar" solo tras ≥1 examen.</summary>
    public int ExamenesSolicitadosCount => motorEvaluacion != null ? motorEvaluacion.ExamenesSolicitadosCount : 0;

    /// <summary>Último texto de diagnóstico que el jugador escribió en el teclado.
    /// El Panel 7 (Registro de Diagnóstico) lo usa al llamar a
    /// <see cref="OnJugadorRegistroDiagnostico"/> para cerrar el caso. Se puede
    /// reescribir cuantas veces se quiera antes de registrar (ver
    /// <see cref="OnJugadorEscribioDiagnostico"/>).</summary>
    public string DiagnosticoRegistrado { get; private set; }

    /// <summary>Último texto de "Indicaciones / manejo" que el jugador escribió en el
    /// teclado (mismo patrón que <see cref="DiagnosticoRegistrado"/>): se reescribe sin
    /// límite antes de registrar, se resetea en <see cref="IniciarCaso"/>, y
    /// <see cref="OnJugadorRegistroDiagnostico"/> lo entrega al MotorEvaluacion. Puede
    /// quedar vacío (el componente Manejo suma 0).</summary>
    public string ManejoRegistrado { get; private set; }

    /// <summary>Fecha y hora reales del sistema en que arrancó el caso (se fija en
    /// <see cref="IniciarCaso"/>). El Panel 7 la muestra como "fecha/hora de
    /// ingreso" de la ficha. NO es un dato de la FichaCaso.</summary>
    public System.DateTime FechaHoraInicio { get; private set; }

    /// <summary>true mientras GestorMenuPausa tiene el panel de pausa abierto.
    /// Congela las transiciones de la máquina de estados (AvanzarFase,
    /// RetrocederFase, OnJugadorRegistroDiagnostico) sin tocar CasoActual ni
    /// lo acumulado en MotorEvaluacion.</summary>
    public bool Pausado { get; private set; }

    /// <summary>Hay un caso a medias: de Introducción a Registro de diagnóstico (ni Inactivo, ni evaluando, ni Debriefing).
    /// Solo lectura; lo usa el registro del estudio para distinguir un caso abandonado de uno terminado.</summary>
    public bool CasoEnCurso =>
        EstadoActual == EstadoCasoClinico.Introduccion || EstadoActual == EstadoCasoClinico.ExamenFisico ||
        EstadoActual == EstadoCasoClinico.Anamnesis || EstadoActual == EstadoCasoClinico.ExamenesComplementarios ||
        EstadoActual == EstadoCasoClinico.RegistroDiagnostico;

    /// <summary>Diagnóstico y manejo tal como se entregaron al MotorEvaluacion al cerrar el caso (solo lectura; null hasta
    /// entonces). Existen porque <see cref="DiagnosticoRegistrado"/> / <see cref="ManejoRegistrado"/> pueden diferir si el
    /// llamador pasó textos explícitos a <see cref="OnJugadorRegistroDiagnostico"/>.</summary>
    public string DiagnosticoEvaluado { get; private set; }
    public string ManejoEvaluado { get; private set; }

    /// <summary>Se dispara cuando un caso EN CURSO se abandona (AbandonarCaso o reiniciarlo con IniciarCaso), ANTES de
    /// reinicializar nada: los contadores y el caso todavía son los del caso que se deja. No cambia ninguna transición.</summary>
    public event Action AlAbandonarCaso;

    /// <summary>Se dispara en cada transición de estado. La UI se suscribe para
    /// mostrar/ocultar paneles.</summary>
    public event Action<EstadoCasoClinico> OnCambioEstado;

    /// <summary>Se dispara una sola vez, al entrar a Debriefing, con el resultado
    /// completo. Único momento en que se revela el desempeño.</summary>
    public event Action<ResultadoCaso> OnCasoFinalizado;

    /// <summary>Texto con el que el paciente responde a una pregunta de anamnesis
    /// (la respuesta específica de la pregunta identificada, o el mensaje genérico
    /// del caso si el texto no matcheó ninguna). NO revela correcto/incorrecto.
    /// Se mantiene por compatibilidad; el Panel de Anamnesis usa
    /// <see cref="OnIntercambioAnamnesis"/>, que además trae la pregunta.</summary>
    public event Action<string> OnRespuestaPacienteRevelada;

    /// <summary>Un intercambio completo de anamnesis por texto libre:
    /// (pregunta que escribió el jugador, respuesta del paciente). Se dispara
    /// junto con <see cref="OnRespuestaPacienteRevelada"/>. Evento nuevo porque el
    /// panel necesita ambos textos EMPAREJADOS: suscribirse por separado a
    /// <c>OnTextoLibreEnviado</c> (pregunta) y <c>OnRespuestaPacienteRevelada</c>
    /// (respuesta) no garantiza el orden — el reenvío de la respuesta ocurre
    /// dentro del handler de la pregunta, y el orden de invocación depende del
    /// orden de suscripción entre GameObjects, que no es determinista.</summary>
    public event Action<string, string> OnIntercambioAnamnesis;

    // ── Ciclo de vida / suscripciones ────────────────────────────────────────

    void OnEnable()
    {
        if (interaccionPaciente == null) return;
        interaccionPaciente.OnRegionExaminada              += OnJugadorExaminoRegion;
        interaccionPaciente.OnPreguntaAlternativaSeleccionada += OnJugadorPreguntoAlternativa;
        interaccionPaciente.OnTextoLibreEnviado            += OnJugadorEscribioPregunta;
        interaccionPaciente.OnDiagnosticoTextoEnviado      += OnJugadorEscribioDiagnostico;
        interaccionPaciente.OnManejoTextoEnviado           += OnJugadorEscribioManejo;
        interaccionPaciente.OnExamenSolicitado             += OnJugadorPidioExamen;
    }

    void OnDisable()
    {
        if (interaccionPaciente == null) return;
        interaccionPaciente.OnRegionExaminada              -= OnJugadorExaminoRegion;
        interaccionPaciente.OnPreguntaAlternativaSeleccionada -= OnJugadorPreguntoAlternativa;
        interaccionPaciente.OnTextoLibreEnviado            -= OnJugadorEscribioPregunta;
        interaccionPaciente.OnDiagnosticoTextoEnviado      -= OnJugadorEscribioDiagnostico;
        interaccionPaciente.OnManejoTextoEnviado           -= OnJugadorEscribioManejo;
        interaccionPaciente.OnExamenSolicitado             -= OnJugadorPidioExamen;
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>Llamado por GestorMenuPausa al abrir/cerrar su panel. Es el
    /// único punto que conoce a InteraccionPaciente (ver doc de la clase), así
    /// que le cascada la pausa en vez de que GestorMenuPausa lo haga directo.</summary>
    public void EstablecerPausa(bool valor)
    {
        Pausado = valor;
        if (interaccionPaciente != null) interaccionPaciente.SetPausado(valor);
    }

    /// <summary>Ítem "Reiniciar caso actual" del menú de pausa: reinicia el
    /// mismo FichaCaso/submodo desde cero (MotorEvaluacion e InteraccionPaciente
    /// se reinicializan vía IniciarCaso; el caso en sí no cambia).</summary>
    public void ReiniciarCasoActual()
    {
        if (casoActual == null) return;
        IniciarCaso(casoActual, submodoActual);
    }

    /// <summary>Arranca (o reinicia) un caso. Llamado desde el Panel de Introducción.</summary>
    public void IniciarCaso(FichaCaso caso, SubmodoCaso submodo)
    {
        if (caso == null)
        {
            Debug.LogError("[GestorCasoClinico] IniciarCaso recibió un caso nulo.", this);
            return;
        }
        if (motorEvaluacion == null || interaccionPaciente == null)
        {
            Debug.LogError("[GestorCasoClinico] Falta asignar MotorEvaluacion o InteraccionPaciente.", this);
            return;
        }

        if (CasoEnCurso) AlAbandonarCaso?.Invoke(); // reinicio de un caso a medias: aún no se ha tocado nada del caso anterior

        casoActual    = caso;
        submodoActual = submodo;
        DiagnosticoRegistrado = null;
        ManejoRegistrado = null;
        DiagnosticoEvaluado = null;
        ManejoEvaluado = null;
        _intencionesLibres.Clear();
        FechaHoraInicio = System.DateTime.Now;
        if (entradaDispositivo != null) entradaDispositivo.ReiniciarRegistro();

        motorEvaluacion.Inicializar(caso);
        interaccionPaciente.Inicializar(caso, submodo);

        TransicionarA(EstadoCasoClinico.Introduccion);
    }

    /// <summary>Avanza el flujo diegético a la siguiente fase. Lo llaman los
    /// botones "Continuar a ..." de la UI, en el orden del enum.</summary>
    public void AvanzarFase()
    {
        if (Pausado) return;
        switch (EstadoActual)
        {
            case EstadoCasoClinico.Introduccion:            TransicionarA(EstadoCasoClinico.ExamenFisico); break;
            case EstadoCasoClinico.ExamenFisico:            TransicionarA(EstadoCasoClinico.Anamnesis); break;
            case EstadoCasoClinico.Anamnesis:               TransicionarA(EstadoCasoClinico.ExamenesComplementarios); break;
            case EstadoCasoClinico.ExamenesComplementarios: TransicionarA(EstadoCasoClinico.RegistroDiagnostico); break;
            default:
                Debug.LogWarning($"[GestorCasoClinico] AvanzarFase() no aplica en estado {EstadoActual}.", this);
                break;
        }
    }

    /// <summary>Retrocede una fase del flujo diegético (botón "◀ Atrás").
    /// Desde Introducción vuelve a Inactivo (reaparece la selección de submodo).
    /// Los registros acumulados en MotorEvaluacion NO se pierden al retroceder
    /// entre fases del caso; solo se reinician si se abandona el caso y se
    /// vuelve a iniciar.</summary>
    public void RetrocederFase()
    {
        if (Pausado) return;
        switch (EstadoActual)
        {
            case EstadoCasoClinico.Introduccion:            AbandonarCaso(); break;
            case EstadoCasoClinico.ExamenFisico:            TransicionarA(EstadoCasoClinico.Introduccion); break;
            case EstadoCasoClinico.Anamnesis:               TransicionarA(EstadoCasoClinico.ExamenFisico); break;
            case EstadoCasoClinico.ExamenesComplementarios: TransicionarA(EstadoCasoClinico.Anamnesis); break;
            case EstadoCasoClinico.RegistroDiagnostico:     TransicionarA(EstadoCasoClinico.ExamenesComplementarios); break;
            default:
                Debug.LogWarning($"[GestorCasoClinico] RetrocederFase() no aplica en estado {EstadoActual}.", this);
                break;
        }
    }

    /// <summary>True si el botón "« VOLVER" aplica en el estado actual: Anamnesis,
    /// Exámenes y Registro. Desde Hallazgos (ExamenFisico) no se retrocede.</summary>
    public bool PuedeVolver =>
        !Pausado &&
        (EstadoActual == EstadoCasoClinico.Anamnesis
         || EstadoActual == EstadoCasoClinico.ExamenesComplementarios
         || EstadoActual == EstadoCasoClinico.RegistroDiagnostico);

    /// <summary>Botón "« VOLVER": pasa a la fase anterior (Registro→Exámenes→Anamnesis→
    /// Examen físico) SIN abandonar el caso: no emite AlAbandonarCaso, no toca el
    /// motor de evaluación, el historial de anamnesis, los exámenes pedidos, los
    /// textos de diagnóstico/manejo ni la duración. Fuera de esas tres fases (o con el
    /// menú de pausa abierto) no hace nada.</summary>
    public void VolverAFaseAnterior()
    {
        if (!PuedeVolver) return;
        switch (EstadoActual)
        {
            case EstadoCasoClinico.Anamnesis:               TransicionarA(EstadoCasoClinico.ExamenFisico); break;
            case EstadoCasoClinico.ExamenesComplementarios: TransicionarA(EstadoCasoClinico.Anamnesis); break;
            case EstadoCasoClinico.RegistroDiagnostico:     TransicionarA(EstadoCasoClinico.ExamenesComplementarios); break;
        }
    }

    /// <summary>Abandona el caso en curso y vuelve a la selección de submodo
    /// (estado Inactivo). Botón "Salir del caso".</summary>
    public void AbandonarCaso()
    {
        if (EstadoActual == EstadoCasoClinico.Inactivo) return;
        if (CasoEnCurso) AlAbandonarCaso?.Invoke();
        if (interaccionPaciente != null) interaccionPaciente.DetenerDeteccion();
        TransicionarA(EstadoCasoClinico.Inactivo);
    }

    /// <summary>Cierre del caso: el jugador registró diagnóstico y manejo (ambos texto
    /// libre). Corre la evaluación completa y pasa a Debriefing. Con argumentos
    /// vacíos/null se usan los textos que el jugador escribió en el teclado
    /// (<see cref="DiagnosticoRegistrado"/> / <see cref="ManejoRegistrado"/>).</summary>
    public void OnJugadorRegistroDiagnostico(string diagnostico, string manejo = null)
    {
        if (Pausado) return;
        if (EstadoActual == EstadoCasoClinico.Evaluando || EstadoActual == EstadoCasoClinico.Debriefing)
            return; // ya se está evaluando / evaluó

        string dx = string.IsNullOrWhiteSpace(diagnostico) ? DiagnosticoRegistrado : diagnostico;
        string mj = string.IsNullOrWhiteSpace(manejo) ? ManejoRegistrado : manejo;
        DiagnosticoEvaluado = dx;
        ManejoEvaluado = mj;

        TransicionarA(EstadoCasoClinico.Evaluando);

        ResultadoCaso resultado = motorEvaluacion.EvaluarCasoCompleto(dx, mj, submodoActual);
        resultado.dispositivoEntrada = entradaDispositivo != null ? entradaDispositivo.Dispositivo : DispositivoEntrada.Manos;

        TransicionarA(EstadoCasoClinico.Debriefing);
        OnCasoFinalizado?.Invoke(resultado);
    }

    /// <summary>Llamado por el botón "Volver al menú" del debriefing.</summary>
    public void FinalizarCaso() => TransicionarA(EstadoCasoClinico.Finalizado);

    // ── Reenvío de eventos del jugador al MotorEvaluacion ─────────────────────
    // El Gestor es el intermediario: InteraccionPaciente nunca toca al Motor.

    private void OnJugadorExaminoRegion(string idRegion) =>
        motorEvaluacion.RegistrarExamenRegion(idRegion);

    private void OnJugadorPreguntoAlternativa(string idPregunta) =>
        motorEvaluacion.RegistrarPreguntaAlternativa(idPregunta);

    /// <summary>Resolución de anamnesis por texto libre, en este orden (determinista, sin IA ni red):
    /// 1) las preguntas clave del caso EN ORDEN con sus conjuntos de palabras clave: la primera que matchee revela su
    ///    respuesta y se marca como realizada (misma vía que <c>pregunto:&lt;id&gt;</c>);
    /// 2) si no coincide ninguna, la intención más específica del <see cref="CatalogoIntenciones"/>. Si está vinculada a una
    ///    pregunta clave (<see cref="PreguntaAnamnesis.intencionesQueSatisface"/>) se registra ESA pregunta y se responde con
    ///    su respuestaPaciente; si no, se responde con el campo de la historia del paciente, SIN puntaje;
    /// 3) si nada coincide, la respuesta genérica del caso (<see cref="FichaCaso.respuestaGenericaSinMatch"/>) y la pregunta
    ///    se anota para ampliar el vocabulario.
    /// El puntaje depende SOLO de las preguntas clave reconocidas. Nunca revela correcto/incorrecto.</summary>
    private void OnJugadorEscribioPregunta(string textoLibre)
    {
        if (Pausado) return;
        if (casoActual == null || string.IsNullOrWhiteSpace(textoLibre)) return;

        // 1) Preguntas clave (matching tolerante: sin signos de puntuación y con una edición en palabras de 5+ letras).
        foreach (var pregunta in casoActual.preguntasClave)
        {
            if (pregunta == null) continue;
            if (!motorEvaluacion.ContienePalabraClave(textoLibre, pregunta.conjuntosPalabrasClave, true)) continue;

            motorEvaluacion.RegistrarPreguntaAlternativa(pregunta.idPregunta);
            OnRespuestaPacienteRevelada?.Invoke(pregunta.respuestaPaciente);
            OnIntercambioAnamnesis?.Invoke(textoLibre, pregunta.respuestaPaciente);
            return;
        }

        // 2) Intenciones del catálogo.
        var intencion = catalogoIntenciones != null ? catalogoIntenciones.Resolver(textoLibre, motorEvaluacion) : null;
        if (intencion != null)
        {
            var vinculada = BuscarPreguntaVinculada(intencion.id);
            string respuesta;
            if (vinculada != null)
            {
                motorEvaluacion.RegistrarPreguntaAlternativa(vinculada.idPregunta);   // puntúa, igual que si la hubiera dicho con sus palabras
                respuesta = vinculada.respuestaPaciente;
            }
            else
            {
                _intencionesLibres.Add(intencion.id);                                  // habilita Continuar, no puntúa
                respuesta = catalogoIntenciones.Responder(intencion, casoActual);
            }
            OnRespuestaPacienteRevelada?.Invoke(respuesta);
            OnIntercambioAnamnesis?.Invoke(textoLibre, respuesta);
            return;
        }

        // 3) Fallback.
        string generica = string.IsNullOrEmpty(casoActual.respuestaGenericaSinMatch)
            ? "Disculpe, no le entendí bien. ¿Me lo puede preguntar de otra forma?"
            : casoActual.respuestaGenericaSinMatch;
        if (registrarSinReconocer) RegistrarSinReconocer(textoLibre);
        OnRespuestaPacienteRevelada?.Invoke(generica);
        OnIntercambioAnamnesis?.Invoke(textoLibre, generica);
    }

    private PreguntaAnamnesis BuscarPreguntaVinculada(string idIntencion)
    {
        foreach (var pregunta in casoActual.preguntasClave)
        {
            if (pregunta == null || pregunta.intencionesQueSatisface == null) continue;
            if (pregunta.intencionesQueSatisface.Contains(idIntencion)) return pregunta;
        }
        return null;
    }

    /// <summary>Anota una pregunta sin reconocer: línea [ANAMNESIS] (solo la primera vez de cada texto en esta ejecución) y una
    /// fila en anamnesis_sin_reconocer.csv (cada vez; fecha, idCaso, submodo, texto). Solo corre al enviar un texto: no hay
    /// trabajo por fotograma. Un error de escritura nunca interrumpe el flujo.</summary>
    private void RegistrarSinReconocer(string texto)
    {
        string limpio = texto.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (_sinReconocerYaLogueadas.Add(limpio.ToLowerInvariant()))
            Debug.Log("[ANAMNESIS] sin reconocer | caso=" + casoActual.idCaso + " submodo=" + submodoActual + " texto='" + limpio + "'");
        try
        {
            string ruta = Path.Combine(Application.persistentDataPath, "anamnesis_sin_reconocer.csv");
            _sbCsv.Length = 0;
            if (!File.Exists(ruta)) _sbCsv.Append("fecha,idCaso,submodo,texto\n");
            _sbCsv.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',')
                  .Append(casoActual.idCaso).Append(',').Append(submodoActual).Append(",\"")
                  .Append(limpio.Replace("\"", "\"\"")).Append("\"\n");
            File.AppendAllText(ruta, _sbCsv.ToString(), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ANAMNESIS] no se pudo escribir anamnesis_sin_reconocer.csv: " + e.Message);
        }
    }

    /// <summary>El jugador escribió el diagnóstico en el teclado. Se guarda; el
    /// matching contra <c>conjuntosPalabrasClaveDiagnostico</c> ocurre en
    /// <see cref="MotorEvaluacion.EvaluarCasoCompleto"/> al cerrar el caso.</summary>
    private void OnJugadorEscribioDiagnostico(string textoLibre)
    {
        if (Pausado) return;
        DiagnosticoRegistrado = textoLibre;
    }

    /// <summary>El jugador escribió las indicaciones / manejo en el teclado. Se guarda; el
    /// matching contra los <c>conjuntosPalabrasClave</c> de cada AccionCritica de tipo
    /// Manejo ocurre en <see cref="MotorEvaluacion.EvaluarCasoCompleto"/> al cerrar el caso.</summary>
    private void OnJugadorEscribioManejo(string textoLibre)
    {
        if (Pausado) return;
        ManejoRegistrado = textoLibre;
    }

    private void OnJugadorPidioExamen(string idExamen) =>
        motorEvaluacion.RegistrarExamenSolicitado(idExamen);

    // ── Interno ──────────────────────────────────────────────────────────────

    private void TransicionarA(EstadoCasoClinico nuevoEstado)
    {
        if (EstadoActual == nuevoEstado) return;
        EstadoActual = nuevoEstado;
        Debug.Log($"[GestorCasoClinico] Estado → {nuevoEstado}");
        OnCambioEstado?.Invoke(nuevoEstado);
    }
}

/// <summary>Fases del caso clínico. El orden refleja el flujo diegético.</summary>
public enum EstadoCasoClinico
{
    Inactivo,
    Introduccion,
    ExamenFisico,
    Anamnesis,
    ExamenesComplementarios,
    RegistroDiagnostico,
    Evaluando,
    Debriefing,
    Finalizado
}
