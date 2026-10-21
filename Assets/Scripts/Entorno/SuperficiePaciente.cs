using UnityEngine;

/// <summary>
/// Tablero visual que acompaña al paciente virtual de SceneSimulador (RCP). En VR puro el usuario calibra el
/// paciente encima del maniquí físico con pinch (CalibradorPosicion), que puede quedar en cualquier posición,
/// altura (mesa o suelo) y giro; esta superficie se coloca SOLA debajo de la espalda del paciente en LateUpdate
/// (después de que el calibrador lo mueva), de modo que nunca queda flotando ni hundida.
///
/// Geometría: la cara superior queda a (altura de la espalda - <see cref="margen"/>). La espalda se obtiene de la
/// cadera del paciente (<see cref="caderas"/>) menos <see cref="espaldaBajoCaderas"/> (medido a escala 1 por el
/// builder y escalado con la escala real del paciente). El tablero NO se escala con el paciente: conserva su tamaño.
/// No modifica al paciente ni a CalibradorPosicion. Sin asignaciones por fotograma (solo aritmética de structs).
/// </summary>
[DisallowMultipleComponent]
public class SuperficiePaciente : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Raíz del paciente (Paciente_Completo). Su escala y su giro (yaw) los fija el calibrador.")]
    [SerializeField] private Transform raizPaciente;
    [Tooltip("Hueso de las caderas del paciente (Hips): ancla horizontal del tablero y referencia de la espalda.")]
    [SerializeField] private Transform caderas;

    [Header("Ajuste (medido por el builder con el paciente a escala 1)")]
    [Tooltip("Distancia vertical, en metros y a escala 1, entre las caderas y el punto más bajo de la espalda.")]
    [SerializeField] private float espaldaBajoCaderas = 0.15f;
    [Tooltip("Margen en metros: la cara superior del tablero queda este valor POR DEBAJO de la espalda. 0 = contacto exacto.")]
    [SerializeField] private float margen = 0.003f;
    [Tooltip("Desplazamiento, en metros y a escala 1, del centro del tablero respecto de las caderas, a lo largo del eje del cuerpo (hacia los pies).")]
    [SerializeField] private float desplazamientoLongitudinal = 0.03f;
    [Tooltip("Giro, en grados, entre el yaw de la raíz del paciente y el eje largo del tablero (el eje del cuerpo).")]
    [SerializeField] private float giroRespectoPaciente = 0f;

    [Header("Aspecto")]
    [Tooltip("Renderer del tablero. Se le aplica el tinte con un MaterialPropertyBlock (no se crea ni modifica ningún material).")]
    [SerializeField] private Renderer visual;
    [Tooltip("Tinte multiplicado sobre el color del pack. Un tono oscuro hace legible el paciente holográfico encima.")]
    [SerializeField] private Color tinte = new Color(0.30f, 0.42f, 0.50f);

    static readonly int ID_COLOR_BASE = Shader.PropertyToID("_BaseColor");

    /// <summary>Altura (m) de la cara superior en el último LateUpdate (para pruebas y diagnóstico).</summary>
    public float AlturaSuperior { get; private set; }

    void Awake() => AplicarTinte();

    void LateUpdate() => Actualizar();

    /// <summary>Aplica el tinte (una sola vez al arrancar; también lo llama el builder del Editor).</summary>
    public void AplicarTinte()
    {
        if (visual == null) return;
        var bloque = new MaterialPropertyBlock();
        visual.GetPropertyBlock(bloque);
        bloque.SetColor(ID_COLOR_BASE, tinte);
        visual.SetPropertyBlock(bloque);
    }

    /// <summary>Coloca el tablero ya (lo usa LateUpdate y el builder del Editor).</summary>
    public void Actualizar()
    {
        if (raizPaciente == null || caderas == null) return;

        float escala = raizPaciente.lossyScale.y;
        float yaw = raizPaciente.eulerAngles.y + giroRespectoPaciente;
        float rad = yaw * Mathf.Deg2Rad;
        // Eje largo del tablero (local +X tras girar yaw alrededor de Y): (cos, 0, -sin)
        float ejeX = Mathf.Cos(rad), ejeZ = -Mathf.Sin(rad);

        Vector3 h = caderas.position;
        float d = desplazamientoLongitudinal * escala;
        float espalda = h.y - espaldaBajoCaderas * escala;
        AlturaSuperior = espalda - margen;

        transform.SetPositionAndRotation(
            new Vector3(h.x + ejeX * d, AlturaSuperior, h.z + ejeZ * d),
            Quaternion.Euler(0f, yaw, 0f));
    }
}
