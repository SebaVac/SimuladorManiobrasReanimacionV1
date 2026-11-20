using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Registro del estudio para el módulo de Razonamiento Clínico (SceneDiagnostico): escribe UNA fila en
/// <c>resultados_diagnostico.csv</c> (<see cref="RegistroResultados"/>) por cada caso que termina o se abandona.
///
///  - Completo: se escribe al llegar al Debriefing (<see cref="GestorCasoClinico.OnCasoFinalizado"/>), con el
///    <see cref="ResultadoCaso"/> exacto (puntajes, errores, diagnóstico correcto…). Una sola vez por caso.
///  - Abandonado: un caso a medias que se deja (botón «Salir del caso», «Atrás» desde la introducción del caso, «Reiniciar
///    caso actual» o «Abandonar y volver al menú principal» del menú de pausa, que descarga la escena). Lleva los datos
///    parciales (sin puntajes: no hubo evaluación). Una caída abrupta (apagar el visor) no escribe nada.
///
/// No cambia ninguna lógica del módulo: solo ESCUCHA eventos públicos (GestorCasoClinico, InteraccionPaciente y
/// GestorMenuPausa) y lleva su propio espejo de lo que ResultadoCaso no trae (regiones examinadas, exámenes pedidos,
/// textos escritos, intercambios de anamnesis sin reconocer, duración sin pausas). Todo es por eventos: sin trabajo por
/// fotograma. Nunca lanza (RegistroResultados atrapa los errores de E/S).
///
/// Al descargar la escena Unity puede destruir los demás componentes antes que a este (su «== null» es true pero sus campos
/// gestionados siguen vivos), por eso el volcado de salida (OnDisable) usa solo referencias gestionadas y datos que este
/// componente ya tenía guardados.
/// </summary>
public class RegistroCasoClinico : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GestorCasoClinico gestorCaso;
    [SerializeField] private InteraccionPaciente interaccionPaciente;
    [SerializeField] private GestorMenuPausa menuPausa;
    [SerializeField] private PosePaciente posePaciente;
    [Tooltip("Opcional: dispositivo de entrada para los casos abandonados (en los completos viene en ResultadoCaso).")]
    [SerializeField] private EntradaControlesTouch entradaDispositivo;

    [Header("Protocolo de estudio")]
    [Tooltip("Escribe resultados_diagnostico.csv (una fila por caso Completo o Abandonado). En false no se guarda nada.")]
    [SerializeField] private bool guardarResultados = true;

    public const string RESPUESTA_GENERICA_DEFECTO = "Disculpe, no le entendí bien. ¿Me lo puede preguntar de otra forma?";

    private readonly List<string> _regiones = new List<string>();
    private readonly List<string> _examenes = new List<string>();
    private string _dxEscrito, _mjEscrito;
    private int _clave, _intercambios, _sinReconocer;
    private float _t0, _segPausa, _tPausaIni;
    private bool _enPausa, _filaEscrita, _cerrandoApp, _guardadoEsteCaso;

    /// <summary>Se escribió (bien o mal) una fila para el caso actual. Lo usa el Debriefing para su línea de estado.</summary>
    public bool GuardadoEsteCaso => _guardadoEsteCaso;
    public bool GuardarResultados => guardarResultados;
    public bool UltimoGuardadoOk { get; private set; }
    /// <summary>Última fila escrita (o que se intentó escribir), sin salto de línea. Solo para verificación.</summary>
    public string UltimaFila { get; private set; }
    /// <summary>(guardadoOk, estado) tras cada escritura.</summary>
    public event Action<bool, string> AlGuardar;

    void OnEnable()
    {
        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado       += AlCambioEstado;
            gestorCaso.OnCasoFinalizado     += AlCasoFinalizado;
            gestorCaso.AlAbandonarCaso      += AlAbandonarCaso;
            gestorCaso.OnIntercambioAnamnesis += AlIntercambioAnamnesis;
        }
        if (interaccionPaciente != null)
        {
            interaccionPaciente.OnRegionExaminada         += AlRegionExaminada;
            interaccionPaciente.OnExamenSolicitado        += AlExamenSolicitado;
            interaccionPaciente.OnDiagnosticoTextoEnviado += AlDiagnosticoEscrito;
            interaccionPaciente.OnManejoTextoEnviado      += AlManejoEscrito;
        }
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += AlAbrirMenu;
            menuPausa.AlCerrarMenu += AlCerrarMenu;
        }
        Application.quitting += AlCerrarAplicacion;
    }

    void OnDisable()
    {
        // Salida de la escena con un caso a medias («Abandonar y volver al menú principal» descarga la escena sin pasar por
        // AbandonarCaso): se registra como Abandonado. No al cerrar la aplicación ni al detener Play.
        if (guardarResultados && !_cerrandoApp && !_filaEscrita && (object)gestorCaso != null && gestorCaso.CasoEnCurso)
            Escribir(RegistroResultados.ESTADO_ABANDONADO, null);

        if (gestorCaso != null)
        {
            gestorCaso.OnCambioEstado       -= AlCambioEstado;
            gestorCaso.OnCasoFinalizado     -= AlCasoFinalizado;
            gestorCaso.AlAbandonarCaso      -= AlAbandonarCaso;
            gestorCaso.OnIntercambioAnamnesis -= AlIntercambioAnamnesis;
        }
        if (interaccionPaciente != null)
        {
            interaccionPaciente.OnRegionExaminada         -= AlRegionExaminada;
            interaccionPaciente.OnExamenSolicitado        -= AlExamenSolicitado;
            interaccionPaciente.OnDiagnosticoTextoEnviado -= AlDiagnosticoEscrito;
            interaccionPaciente.OnManejoTextoEnviado      -= AlManejoEscrito;
        }
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= AlAbrirMenu;
            menuPausa.AlCerrarMenu -= AlCerrarMenu;
        }
        Application.quitting -= AlCerrarAplicacion;
    }

    private void AlCerrarAplicacion() { _cerrandoApp = true; }

    // ── Espejo de lo que ocurre en el caso ───────────────────────────────────

    private void AlCambioEstado(EstadoCasoClinico estado)
    {
        if (estado != EstadoCasoClinico.Introduccion) return;
        // Caso nuevo (o reinicio del mismo): se parte de cero.
        _regiones.Clear(); _examenes.Clear();
        _dxEscrito = null; _mjEscrito = null;
        _clave = _intercambios = _sinReconocer = 0;
        _t0 = Time.realtimeSinceStartup; _segPausa = 0f; _tPausaIni = 0f; _enPausa = false;
        _filaEscrita = false; _guardadoEsteCaso = false;
    }

    private void AlRegionExaminada(string idRegion)
    {
        if (!string.IsNullOrEmpty(idRegion) && !_regiones.Contains(idRegion)) _regiones.Add(idRegion);
    }

    private void AlExamenSolicitado(string idExamen)
    {
        if (!string.IsNullOrEmpty(idExamen) && !_examenes.Contains(idExamen)) _examenes.Add(idExamen);
    }

    private void AlDiagnosticoEscrito(string texto) { _dxEscrito = texto; }
    private void AlManejoEscrito(string texto) { _mjEscrito = texto; }

    // El Gestor invoca este evento DESPUÉS de registrar la pregunta en el Motor, así que los contadores ya están al día.
    private void AlIntercambioAnamnesis(string pregunta, string respuesta)
    {
        _clave = gestorCaso.PreguntasReconocidasCount;
        _intercambios = gestorCaso.IntercambiosReconocidosCount;
        var caso = gestorCaso.CasoActual;
        string generica = caso != null && !string.IsNullOrEmpty(caso.respuestaGenericaSinMatch) ? caso.respuestaGenericaSinMatch : RESPUESTA_GENERICA_DEFECTO;
        if (respuesta == generica) _sinReconocer++;   // el paciente contestó con la respuesta genérica = pregunta sin reconocer
    }

    private void AlAbrirMenu()  { if (!_enPausa) { _enPausa = true; _tPausaIni = Time.realtimeSinceStartup; } }
    private void AlCerrarMenu() { if (_enPausa) { _enPausa = false; _segPausa += Time.realtimeSinceStartup - _tPausaIni; } }

    // ── Escritura ────────────────────────────────────────────────────────────

    private void AlCasoFinalizado(ResultadoCaso resultado)
    {
        if (!guardarResultados || _filaEscrita) return; // una sola fila por caso
        Escribir(RegistroResultados.ESTADO_COMPLETO, resultado);
    }

    private void AlAbandonarCaso()
    {
        if (!guardarResultados || _filaEscrita) return;
        Escribir(RegistroResultados.ESTADO_ABANDONADO, null);
    }

    private void Escribir(string estado, ResultadoCaso resultado)
    {
        _filaEscrita = true;
        _guardadoEsteCaso = true;
        string fila = RegistroResultados.FilaDiagnostico(Armar(estado, resultado));
        UltimaFila = fila;
        UltimoGuardadoOk = RegistroResultados.Escribir(RegistroResultados.ARCHIVO_DIAGNOSTICO, RegistroResultados.CABECERA_DIAGNOSTICO, fila);
        AlGuardar?.Invoke(UltimoGuardadoOk, estado);
    }

    private RegistroResultados.DatosFilaDiagnostico Armar(string estado, ResultadoCaso r)
    {
        var caso = (object)gestorCaso != null ? gestorCaso.CasoActual : null;
        var d = new RegistroResultados.DatosFilaDiagnostico
        {
            codigo = CodigoParticipante.Actual,
            estado = estado,
            idCaso = caso != null ? caso.idCaso : "",
            submodo = (r != null ? r.submodo : gestorCaso.SubmodoActual).ToString(),
            pose = (object)posePaciente != null ? posePaciente.Actual.ToString() : "",
            dispositivoEntrada = NombreDispositivo(r != null ? r.dispositivoEntrada
                               : ((object)entradaDispositivo != null ? entradaDispositivo.Dispositivo : DispositivoEntrada.Manos)),
            duracionSeg = DuracionSeg(),
            resultado = r,
            diagnosticoEscrito = r != null ? gestorCaso.DiagnosticoEvaluado : _dxEscrito,
            manejoEscrito = r != null ? gestorCaso.ManejoEvaluado : _mjEscrito,
            preguntasClaveReconocidas = r != null ? gestorCaso.PreguntasReconocidasCount : _clave,
            intercambiosReconocidos = r != null ? gestorCaso.IntercambiosReconocidosCount : _intercambios,
            preguntasSinReconocer = _sinReconocer,
            regionesExaminadas = string.Join("|", _regiones.ToArray()),
            examenesPedidos = string.Join("|", _examenes.ToArray()),
        };

        // Clasificación de los exámenes pedidos con la MISMA regla que MotorEvaluacion: Pertinente / Indiferente / NoPertinente
        // según la ficha, y todo examen pedido que la ficha no trae cuenta como no pertinente.
        int pert = 0, indif = 0, nop = 0;
        var nombresFicha = new HashSet<string>();
        if (caso != null && caso.examenesDisponibles != null)
            foreach (var e in caso.examenesDisponibles)
            {
                if (e == null) continue;
                nombresFicha.Add(e.nombreExamen);
                if (!_examenes.Contains(e.nombreExamen)) continue;
                if (e.clasificacion == ClasificacionExamen.Pertinente) pert++;
                else if (e.clasificacion == ClasificacionExamen.Indiferente) indif++;
                else nop++;
            }
        foreach (var pedido in _examenes) if (!nombresFicha.Contains(pedido)) nop++;
        d.examenesPertinentes = pert; d.examenesIndiferentes = indif; d.examenesNoPertinentes = nop;

        if (r != null && r.erroresDetectados != null)
        {
            var ids = new List<string>();
            foreach (var e in r.erroresDetectados) if (e != null) ids.Add(e.idReferenciaCondicion ?? "");
            d.erroresDetectados = string.Join("|", ids.ToArray());
        }
        return d;
    }

    /// <summary>Segundos desde que arrancó el caso hasta ahora, sin contar el tiempo con el menú de pausa abierto.</summary>
    private float DuracionSeg()
    {
        float ahora = Time.realtimeSinceStartup;
        float pausa = _segPausa + (_enPausa ? ahora - _tPausaIni : 0f);
        return Mathf.Max(0f, ahora - _t0 - pausa);
    }

    private static string NombreDispositivo(DispositivoEntrada d)
        => d == DispositivoEntrada.Controles ? "Controles" : d == DispositivoEntrada.Mixto ? "Mixto" : "Manos";
}
