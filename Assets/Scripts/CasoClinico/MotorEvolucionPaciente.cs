using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State machine data-driven de evolución del paciente.
///
/// Desacoplamiento estricto:
///   - No conoce UI, audio ni Animator.
///   - Comunica cambios de fase exclusivamente vía OnFaseCambiada.
///   - Sistemas externos (UI, audio, etc.) se suscriben al evento.
///
/// Restricción de allocations:
///   - Update() solo incrementa timer y compara Time.time — cero allocations.
///   - EvaluarTransiciones() corre por intervalo, no cada frame.
///   - _accionesRealizadas preallocado en Start; crece solo por acción del usuario.
/// </summary>
public class MotorEvolucionPaciente : MonoBehaviour
{
    [Header("Referencias")]
    public GestorCasoClinico gestor;

    [Header("Configuración")]
    [Tooltip("Segundos entre evaluaciones de triggers. 0.5 = 2 evaluaciones por segundo.")]
    public float intervaloEvaluacionSeg = 0.5f;

    // Sistemas externos suscriben aquí para reaccionar a cambios de fase.
    // MotorEvolucionPaciente NUNCA referencia los suscriptores directamente.
    public event Action<FaseClinica> OnFaseCambiada;

    public FaseClinica FaseActual => _faseActual;
    public bool        Activo     => _activo;

    // ── Estado interno ────────────────────────────────────────────────────────

    FaseClinica _faseActual;
    float       _tiempoEnFaseActual;
    float       _tiempoUltimaEvaluacion;
    bool        _activo;

    // Capacidad inicial generosa para evitar resize durante una sesión típica
    readonly List<string> _accionesRealizadas = new List<string>(8);

    // ── Ciclo de vida Unity ───────────────────────────────────────────────────

    void Start()
    {
        if (gestor == null)
        {
            Debug.LogError("[MotorEvolucionPaciente] Falta referencia a GestorCasoClinico.", this);
            return;
        }

        var caso = gestor.ObtenerCasoActivo();
        if (caso == null)
        {
            Debug.LogError("[MotorEvolucionPaciente] GestorCasoClinico no tiene caso activo asignado.", this);
            return;
        }

        IniciarCaso(caso);
    }

    void Update()
    {
        if (!_activo) return;

        // Acumular tiempo en fase — cero allocations
        _tiempoEnFaseActual += Time.deltaTime;

        // Evaluación por intervalo — no polling continuo
        if (Time.time - _tiempoUltimaEvaluacion < intervaloEvaluacionSeg) return;
        _tiempoUltimaEvaluacion = Time.time;

        EvaluarTransiciones();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia o reinicia el motor con el caso dado.
    /// Puede llamarse en tiempo de ejecución para cambiar de caso.
    /// </summary>
    public void IniciarCaso(FichaCaso caso)
    {
        _accionesRealizadas.Clear();
        _tiempoEnFaseActual      = 0f;
        _tiempoUltimaEvaluacion  = 0f;

        var faseInicial = caso.ObtenerFase(caso.faseInicialId);
        if (faseInicial == null)
        {
            Debug.LogError(
                $"[MotorEvolucionPaciente] Fase inicial '{caso.faseInicialId}' " +
                $"no encontrada en '{caso.nombre}'.", this);
            return;
        }

        _faseActual = faseInicial;
        _activo     = true;
        OnFaseCambiada?.Invoke(_faseActual);
    }

    /// <summary>
    /// Notifica al motor que el alumno realizó una acción crítica.
    /// Llamar desde InteraccionPaciente, UI, o cualquier sistema de detección.
    /// Idempotente: registrar la misma acción dos veces no tiene efecto.
    /// </summary>
    public void ReportarAccionRealizada(string accionId)
    {
        if (!_activo || string.IsNullOrEmpty(accionId)) return;
        if (!_accionesRealizadas.Contains(accionId))
            _accionesRealizadas.Add(accionId);
    }

    /// <summary>Detiene la evaluación de triggers sin limpiar el estado.</summary>
    public void Detener() => _activo = false;

    // ── Lógica interna ────────────────────────────────────────────────────────

    void EvaluarTransiciones()
    {
        if (_faseActual?.transiciones == null) return;

        var transiciones = _faseActual.transiciones;
        for (int i = 0; i < transiciones.Count; i++)
        {
            if (!EsTriggerCumplido(transiciones[i])) continue;
            TransicionarA(transiciones[i].faseDestinoId);
            return; // Una sola transición por ciclo — el orden en la lista define prioridad
        }
    }

    bool EsTriggerCumplido(TriggerTransicion trigger)
    {
        switch (trigger.tipo)
        {
            case TriggerTransicion.TipoTrigger.AccionCriticaRealizada:
                return _accionesRealizadas.Contains(trigger.referenciaAccionId);

            case TriggerTransicion.TipoTrigger.AccionCriticaOmitida:
                // Dispara cuando el tiempo de fase expiró Y la acción NO fue realizada
                float limite = _faseActual.tiempoMaximoSegundos;
                return limite > 0f
                    && _tiempoEnFaseActual >= limite
                    && !_accionesRealizadas.Contains(trigger.referenciaAccionId);

            case TriggerTransicion.TipoTrigger.TiempoExcedido:
                return _faseActual.tiempoMaximoSegundos > 0f
                    && _tiempoEnFaseActual >= _faseActual.tiempoMaximoSegundos;

            case TriggerTransicion.TipoTrigger.SignoVitalFueraDeRango:
                // Estructura y campos declarados. Evaluación activa en próxima iteración
                // con ReportarSignoVital(string signoId, float valor).
                return false;

            default:
                return false;
        }
    }

    void TransicionarA(string idFaseDestino)
    {
        var nuevaFase = gestor.ObtenerCasoActivo().ObtenerFase(idFaseDestino);
        if (nuevaFase == null)
        {
            Debug.LogWarning(
                $"[MotorEvolucionPaciente] Fase destino '{idFaseDestino}' no encontrada en el caso activo.", this);
            return;
        }

        string nombreAnterior = _faseActual.nombre;
        _faseActual             = nuevaFase;
        _tiempoEnFaseActual     = 0f;
        _accionesRealizadas.Clear(); // Cada fase evalúa sus propias acciones desde cero

        Debug.Log($"[MotorEvolucionPaciente] {nombreAnterior} → {_faseActual.nombre}  (t={Time.time:F1}s)");
        OnFaseCambiada?.Invoke(_faseActual);
    }
}
