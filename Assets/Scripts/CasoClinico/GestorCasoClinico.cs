using System;
using System.Collections.Generic;
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
    [SerializeField] private SubmodoCaso submodoActual = SubmodoCaso.Guiado;

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

    /// <summary>Fecha y hora reales del sistema en que arrancó el caso (se fija en
    /// <see cref="IniciarCaso"/>). El Panel 7 la muestra como "fecha/hora de
    /// ingreso" de la ficha. NO es un dato de la FichaCaso.</summary>
    public System.DateTime FechaHoraInicio { get; private set; }

    /// <summary>true mientras GestorMenuPausa tiene el panel de pausa abierto.
    /// Congela las transiciones de la máquina de estados (AvanzarFase,
    /// RetrocederFase, OnJugadorRegistroDiagnostico) sin tocar CasoActual ni
    /// lo acumulado en MotorEvaluacion.</summary>
    public bool Pausado { get; private set; }

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
        interaccionPaciente.OnExamenSolicitado             += OnJugadorPidioExamen;
    }

    void OnDisable()
    {
        if (interaccionPaciente == null) return;
        interaccionPaciente.OnRegionExaminada              -= OnJugadorExaminoRegion;
        interaccionPaciente.OnPreguntaAlternativaSeleccionada -= OnJugadorPreguntoAlternativa;
        interaccionPaciente.OnTextoLibreEnviado            -= OnJugadorEscribioPregunta;
        interaccionPaciente.OnDiagnosticoTextoEnviado      -= OnJugadorEscribioDiagnostico;
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

        casoActual    = caso;
        submodoActual = submodo;
        DiagnosticoRegistrado = null;
        FechaHoraInicio = System.DateTime.Now;

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

    /// <summary>Abandona el caso en curso y vuelve a la selección de submodo
    /// (estado Inactivo). Botón "Salir del caso".</summary>
    public void AbandonarCaso()
    {
        if (EstadoActual == EstadoCasoClinico.Inactivo) return;
        if (interaccionPaciente != null) interaccionPaciente.DetenerDeteccion();
        TransicionarA(EstadoCasoClinico.Inactivo);
    }

    /// <summary>Cierre del caso: el jugador registró diagnóstico y conductas de
    /// manejo. Corre la evaluación completa y pasa a Debriefing.</summary>
    public void OnJugadorRegistroDiagnostico(string diagnostico, List<string> manejo)
    {
        if (Pausado) return;
        if (EstadoActual == EstadoCasoClinico.Evaluando || EstadoActual == EstadoCasoClinico.Debriefing)
            return; // ya se está evaluando / evaluó

        // Si no llega diagnóstico explícito, se usa el que el jugador escribió en el teclado.
        string dx = string.IsNullOrWhiteSpace(diagnostico) ? DiagnosticoRegistrado : diagnostico;

        TransicionarA(EstadoCasoClinico.Evaluando);

        ResultadoCaso resultado = motorEvaluacion.EvaluarCasoCompleto(dx, manejo);

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

    /// <summary>Resolución de anamnesis por texto libre: recorre las preguntas del
    /// caso EN ORDEN, y en el primer conjunto de palabras clave que matchee revela
    /// la respuesta de esa pregunta y la marca como realizada (misma vía que
    /// <c>pregunto:&lt;id&gt;</c>). Si nada matchea, respuesta genérica del caso y
    /// no se marca nada. Nunca revela correcto/incorrecto.</summary>
    private void OnJugadorEscribioPregunta(string textoLibre)
    {
        if (Pausado) return;
        if (casoActual == null || string.IsNullOrWhiteSpace(textoLibre)) return;

        foreach (var pregunta in casoActual.preguntasClave)
        {
            if (pregunta == null) continue;
            if (!motorEvaluacion.ContienePalabraClave(textoLibre, pregunta.conjuntosPalabrasClave)) continue;

            motorEvaluacion.RegistrarPreguntaAlternativa(pregunta.idPregunta);
            OnRespuestaPacienteRevelada?.Invoke(pregunta.respuestaPaciente);
            OnIntercambioAnamnesis?.Invoke(textoLibre, pregunta.respuestaPaciente);
            return;
        }

        string generica = string.IsNullOrEmpty(casoActual.respuestaGenericaSinMatch)
            ? "No estoy segura de a qué se refiere con eso."
            : casoActual.respuestaGenericaSinMatch;
        OnRespuestaPacienteRevelada?.Invoke(generica);
        OnIntercambioAnamnesis?.Invoke(textoLibre, generica);
    }

    /// <summary>El jugador escribió el diagnóstico en el teclado. Se guarda; el
    /// matching contra <c>conjuntosPalabrasClaveDiagnostico</c> ocurre en
    /// <see cref="MotorEvaluacion.EvaluarCasoCompleto"/> al cerrar el caso.</summary>
    private void OnJugadorEscribioDiagnostico(string textoLibre)
    {
        if (Pausado) return;
        DiagnosticoRegistrado = textoLibre;
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
