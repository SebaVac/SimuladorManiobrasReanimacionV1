using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Arnés de validación end-to-end de la SECCIÓN 1 (datos + lógica), sin UI.
///
/// Uso:
///   1. Crea una escena vacía.
///   2. Crea un GameObject vacío y agrégale este componente.
///   3. Asigna 'casoDePrueba' (p. ej. CasoEjemploPlaceholder.asset).
///   4. Play. Revisa la Consola: construye el grafo de objetos, recorre todas
///      las fases, simula acciones del jugador (incluida proximidad real
///      mano↔región) y termina imprimiendo el ResultadoCaso.
///
/// Solo para pruebas — no forma parte del módulo en producción. Usa reflexión
/// para inyectar las referencias privadas [SerializeField]; aceptable en un
/// arnés de test, no en código de runtime.
/// </summary>
public class ValidadorFlujoCasoClinico : MonoBehaviour
{
    [Header("Caso a validar (placeholder)")]
    public FichaCaso casoDePrueba;

    [Header("Submodo")]
    public SubmodoCaso submodo = SubmodoCaso.Guiado;

    private GestorCasoClinico gestor;
    private InteraccionPaciente interaccion;
    private MotorEvaluacion motor;

    private Transform manoSimulada;
    private readonly List<Transform> regionesSimuladas = new List<Transform>();

    void Start()
    {
        if (casoDePrueba == null)
        {
            Debug.LogError("[Validador] Asigna 'casoDePrueba' en el Inspector.", this);
            return;
        }

        ConstruirGrafo();
        SuscribirLogs();
        StartCoroutine(EjecutarPrueba());
    }

    // ── Construcción del grafo de objetos ────────────────────────────────────

    private void ConstruirGrafo()
    {
        motor       = new GameObject("MotorEvaluacion (prueba)").AddComponent<MotorEvaluacion>();
        interaccion = new GameObject("InteraccionPaciente (prueba)").AddComponent<InteraccionPaciente>();
        gestor      = new GameObject("GestorCasoClinico (prueba)").AddComponent<GestorCasoClinico>();

        // Regiones anatómicas simuladas: un Transform por hallazgo, separados 10 m
        // entre sí para que la mano solo esté cerca de uno a la vez.
        var raiz = new GameObject("Regiones (prueba)").transform;
        for (int i = 0; i < casoDePrueba.hallazgosPorRegion.Count; i++)
        {
            var t = new GameObject(casoDePrueba.hallazgosPorRegion[i].regionAnatomica).transform;
            t.SetParent(raiz);
            t.position = new Vector3(i * 10f, 0f, 0f);
            regionesSimuladas.Add(t);
        }
        manoSimulada = new GameObject("ManoSimulada (prueba)").transform;
        manoSimulada.position = new Vector3(0f, 1000f, 0f); // lejos de todo al inicio

        InyectarPrivado(interaccion, "regionesAnatomicas", regionesSimuladas.ToArray());
        InyectarPrivado(interaccion, "manoDerecha", manoSimulada);

        InyectarPrivado(gestor, "interaccionPaciente", interaccion);
        InyectarPrivado(gestor, "motorEvaluacion", motor);

        // Reactivar para que OnEnable del Gestor haga las suscripciones ya con
        // las referencias inyectadas.
        gestor.enabled = false;
        gestor.enabled = true;
    }

    private static void InyectarPrivado(object destino, string campo, object valor)
    {
        FieldInfo fi = destino.GetType().GetField(
            campo, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi == null) { Debug.LogError($"[Validador] Campo privado no encontrado: '{campo}'."); return; }
        fi.SetValue(destino, valor);
    }

    // ── Logs de eventos ─────────────────────────────────────────────────────

    private void SuscribirLogs()
    {
        gestor.OnCambioEstado   += e => Debug.Log($"[Validador] → Estado: {e}");
        gestor.OnCasoFinalizado += ImprimirResultado;

        interaccion.OnRegionExaminada                 += r => Debug.Log($"[Validador]   (evento) región examinada: {r}");
        interaccion.OnPreguntaAlternativaSeleccionada += p => Debug.Log($"[Validador]   (evento) pregunta alternativa: {p}");
        interaccion.OnTextoLibreEnviado               += t => Debug.Log($"[Validador]   (evento) texto libre: \"{t}\"");
        interaccion.OnExamenSolicitado               += x => Debug.Log($"[Validador]   (evento) examen solicitado: {x}");
    }

    // ── Simulación de una partida completa ──────────────────────────────────

    private IEnumerator EjecutarPrueba()
    {
        var caso = casoDePrueba;
        Debug.Log("──────── [Validador] INICIO DE PRUEBA ────────");

        gestor.IniciarCaso(caso, submodo);      // → Introduccion
        gestor.AvanzarFase();                   // → ExamenFisico

        // Proximidad real: mover la mano sobre cada región y esperar un frame
        // para que InteraccionPaciente.Update() dispare OnRegionExaminada.
        for (int i = 0; i < regionesSimuladas.Count; i++)
        {
            manoSimulada.position = regionesSimuladas[i].position;
            yield return null;
            yield return null;
        }
        manoSimulada.position = new Vector3(0f, 1000f, 0f);
        yield return null;

        gestor.AvanzarFase();                   // → Anamnesis

        // Mitad por alternativa, mitad por texto libre.
        for (int i = 0; i < caso.preguntasClave.Count; i++)
        {
            var q = caso.preguntasClave[i];
            if (i % 2 == 0)
                interaccion.SeleccionarPreguntaAlternativa(q.idPregunta);
            else if (q.palabrasClave != null && q.palabrasClave.Count > 0)
                interaccion.EnviarTextoLibre("el paciente refiere " + q.palabrasClave[0]);
        }

        gestor.AvanzarFase();                   // → ExamenesComplementarios

        foreach (var ex in caso.examenesDisponibles)
            interaccion.SolicitarExamen(ex.nombreExamen);

        gestor.AvanzarFase();                   // → RegistroDiagnostico

        var manejo = new List<string>();
        foreach (var a in caso.accionesCriticas)
            if (a.tipo == TipoAccion.Manejo) manejo.Add(a.idReferencia);

        gestor.OnJugadorRegistroDiagnostico(caso.diagnosticoEsperado, manejo); // → Evaluando → Debriefing
        gestor.FinalizarCaso();                 // → Finalizado
    }

    private void ImprimirResultado(ResultadoCaso r)
    {
        Debug.Log("──────── [Validador] RESULTADO ────────");
        Debug.Log($"Puntaje: {r.puntajeTotal} / {r.puntajeMaximo}");
        Debug.Log($"Diagnóstico correcto: {r.diagnosticoCorrecto}");
        Debug.Log($"Acciones cumplidas: {r.accionesCumplidas.Count}");
        foreach (var a in r.accionesCumplidas) Debug.Log($"   OK  [{a.tipo}] {a.descripcion} (peso {a.peso})");
        Debug.Log($"Acciones omitidas: {r.accionesOmitidas.Count}");
        foreach (var a in r.accionesOmitidas) Debug.Log($"   --  [{a.tipo}] {a.descripcion} (peso {a.peso})");
        Debug.Log($"Errores frecuentes detectados: {r.erroresDetectados.Count}");
        foreach (var e in r.erroresDetectados) Debug.Log($"   !!  {e.descripcion} -> {e.retroalimentacion}");
        Debug.Log("──────── [Validador] FIN ────────");
    }
}
