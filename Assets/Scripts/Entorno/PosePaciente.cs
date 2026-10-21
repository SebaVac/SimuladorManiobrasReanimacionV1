using System;
using UnityEngine;

public enum PosePacienteTipo { Sentado = 0, Acostado = 1 }

/// <summary>
/// Pose del paciente de SceneDiagnostico (Banana Man): <b>Sentado</b> en el borde de la camilla (por defecto) o
/// <b>Acostado</b> (el estado original del proyecto, con la altura ajustada a la camilla). Cada pose guarda, ya
/// calculada por el builder <c>ConstruirBoxConsultaDiagnostico</c>, la posición/rotación de la raíz, la pose local de
/// TODOS los huesos (el rig se mueve rotando huesos, sin Animator), la posición local de los 12 anclajes de región
/// (mismos Transform y nombres exactos, así InteraccionPaciente, MotorEvaluacion y el puntaje no cambian) y la
/// posición de la camilla (sentado la camilla avanza para que el examen quede a una distancia cómoda).
/// Cambiar de pose es copiar esos valores: sin cálculo, sin Animator y sin asignaciones. Se aplica al arrancar y
/// al cambiar el campo en el Inspector.
/// </summary>
[DisallowMultipleComponent]
public class PosePaciente : MonoBehaviour
{
    [Serializable]
    public class ConfiguracionPose
    {
        public Vector3 posicionRaiz;
        public Quaternion rotacionRaiz = Quaternion.identity;
        public Vector3[] posicionesHuesos;
        public Quaternion[] rotacionesHuesos;
        public Vector3[] posicionesAnclajes;
        public Vector3 posicionCamilla;
    }

    [Header("Pose")]
    [Tooltip("Pose con la que arranca la escena. Cambiarla en el Inspector la aplica de inmediato.")]
    [SerializeField] private PosePacienteTipo pose = PosePacienteTipo.Sentado;

    [Header("Rig y marcadores (los llena el builder)")]
    [Tooltip("Todos los Transform del Armature (huesos), en el orden de las configuraciones.")]
    [SerializeField] private Transform[] huesos;
    [Tooltip("Los 12 anclajes de región (hijos de Regiones), en el orden de las configuraciones. No se renombran ni se recrean.")]
    [SerializeField] private Transform[] anclajes;
    [SerializeField] private Transform camilla;

    [Header("Configuraciones guardadas")]
    [SerializeField] private ConfiguracionPose acostado = new ConfiguracionPose();
    [SerializeField] private ConfiguracionPose sentado = new ConfiguracionPose();
    [Tooltip("Posición ORIGINAL de la raíz acostada, antes de bajarla a la camilla (solo referencia; el builder la usa para ser idempotente).")]
    [SerializeField] private Vector3 posicionRaizAcostadaOriginal;

    public PosePacienteTipo Actual { get; private set; } = PosePacienteTipo.Sentado;
    public PosePacienteTipo PoseInicial => pose;

    void Awake() => Aplicar(pose);

    /// <summary>Aplica una pose. Sin asignaciones.</summary>
    public void Aplicar(PosePacienteTipo tipo)
    {
        var c = tipo == PosePacienteTipo.Sentado ? sentado : acostado;
        if (c == null || c.posicionesHuesos == null || huesos == null || c.posicionesHuesos.Length != huesos.Length) return;

        transform.SetPositionAndRotation(c.posicionRaiz, c.rotacionRaiz);
        for (int i = 0; i < huesos.Length; i++)
            if (huesos[i] != null) huesos[i].SetLocalPositionAndRotation(c.posicionesHuesos[i], c.rotacionesHuesos[i]);
        if (anclajes != null && c.posicionesAnclajes != null)
            for (int i = 0; i < anclajes.Length && i < c.posicionesAnclajes.Length; i++)
                if (anclajes[i] != null) anclajes[i].localPosition = c.posicionesAnclajes[i];
        if (camilla != null) camilla.position = c.posicionCamilla;
        Actual = tipo;
    }

    /// <summary>Alterna entre las dos poses.</summary>
    public void Alternar() => Aplicar(Actual == PosePacienteTipo.Sentado ? PosePacienteTipo.Acostado : PosePacienteTipo.Sentado);

#if UNITY_EDITOR
    void OnValidate()
    {
        // Vista previa al cambiar el enum en el Inspector (fuera de Play); diferida para no tocar transforms durante la validación.
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Aplicar(pose); };
    }
#endif
}
