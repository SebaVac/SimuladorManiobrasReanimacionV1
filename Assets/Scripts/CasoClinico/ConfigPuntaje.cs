using UnityEngine;

/// <summary>
/// Pesos y parámetros del modelo de puntaje del modo Evaluación (100 puntos).
/// Único lugar donde viven estos números: <see cref="MotorEvaluacion"/> los lee
/// de aquí y no tiene ninguno hardcodeado. Los valores por defecto de los campos
/// SON el modelo vigente; el asset de escena solo permite ajustarlos.
///
/// Modelo:
///   Diagnóstico ....... todo o nada.
///   Manejo ............ proporcional al peso de las acciones de Manejo cumplidas.
///   Exámenes .......... proporcional a los pertinentes pedidos, menos una
///                       penalización por cada examen NO pertinente pedido.
///   Anamnesis ......... proporcional a las preguntas clave reconocidas.
///   Examen físico ..... proporcional a las regiones relevantes examinadas.
///   Errores frecuentes  resta por error detectado, con tope.
/// Total con piso 0 y techo = suma de los pesos.
/// </summary>
[CreateAssetMenu(fileName = "ConfigPuntaje", menuName = "RazonamientoClinico/Configuración de Puntaje")]
public class ConfigPuntaje : ScriptableObject
{
    [Header("Puntos máximos por componente")]
    public float pesoDiagnostico  = 30f;
    public float pesoManejo       = 25f;
    public float pesoExamenes     = 20f;
    public float pesoAnamnesis    = 15f;
    public float pesoExamenFisico = 10f;

    [Header("Errores frecuentes")]
    [Tooltip("Puntos que resta cada error detectado.")]
    public float penalizacionPorError = 5f;
    [Tooltip("Tope de la resta total por errores.")]
    public float topePenalizacionErrores = 15f;

    [Header("Exámenes")]
    [Tooltip("Penalización por examen NO pertinente pedido, como fracción del valor de " +
             "un examen pertinente (pesoExamenes / pertinentes totales). 0.25 = un cuarto.")]
    public float factorPenalizacionExamenNoPertinente = 0.25f;

    /// <summary>Suma de los pesos de los 5 componentes (hoy 100).</summary>
    public float PesoTotal =>
        pesoDiagnostico + pesoManejo + pesoExamenes + pesoAnamnesis + pesoExamenFisico;
}
