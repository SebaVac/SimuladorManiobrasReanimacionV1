using UnityEngine;

/// <summary>
/// Visuales de los controles Touch: línea fina teal desde cada control hasta el punto de impacto (o hasta el
/// alcance), un pequeño retículo en el impacto y una esfera pequeña en la punta virtual (examen físico).
/// Solo se ve cuando la entrada activa son los controles; con manos todo queda oculto. Reutiliza los
/// LineRenderer y transforms que crea el builder: no instancia nada en Update (cero asignaciones).
/// </summary>
[DefaultExecutionOrder(100)]
public class VisualControlesTouch : MonoBehaviour
{
    [SerializeField] private EntradaControlesTouch entrada;
    [SerializeField] private GestorSeleccionToque arbitro;
    [Tooltip("Una línea por control (0 = izquierdo, 1 = derecho).")]
    [SerializeField] private LineRenderer[] lineas = new LineRenderer[2];
    [Tooltip("Retículo en el punto de impacto, uno por control.")]
    [SerializeField] private Transform[] reticulos = new Transform[2];
    [Tooltip("Esfera en la punta virtual, una por control.")]
    [SerializeField] private Transform[] puntas = new Transform[2];

    private readonly bool[] _lineaVis = new bool[2];
    private readonly bool[] _reticuloVis = new bool[2];
    private readonly bool[] _puntaVis = new bool[2];

    void OnDisable() => OcultarTodo();

    void LateUpdate()
    {
        if (entrada == null || arbitro == null || entrada.Modo != ModoEntrada.Controles)
        {
            OcultarTodo();
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            bool hayRayo = arbitro.ObtenerRayoVisual(i, out Vector3 origen, out Vector3 fin, out bool impacta);
            if (lineas[i] != null)
            {
                if (hayRayo)
                {
                    lineas[i].SetPosition(0, origen);
                    lineas[i].SetPosition(1, fin);
                }
                Fijar(lineas[i].gameObject, _lineaVis, i, hayRayo);
            }
            if (reticulos[i] != null)
            {
                if (hayRayo && impacta) reticulos[i].position = fin;
                Fijar(reticulos[i].gameObject, _reticuloVis, i, hayRayo && impacta);
            }
            if (puntas[i] != null)
            {
                bool hayPunta = entrada.TryPuntaVirtual(i, out Vector3 p);
                if (hayPunta) puntas[i].position = p;
                Fijar(puntas[i].gameObject, _puntaVis, i, hayPunta);
            }
        }
    }

    private static void Fijar(GameObject go, bool[] cache, int i, bool visible)
    {
        if (cache[i] == visible && go.activeSelf == visible) return;
        cache[i] = visible;
        go.SetActive(visible);
    }

    private void OcultarTodo()
    {
        for (int i = 0; i < 2; i++)
        {
            if (lineas != null && i < lineas.Length && lineas[i] != null) Fijar(lineas[i].gameObject, _lineaVis, i, false);
            if (reticulos != null && i < reticulos.Length && reticulos[i] != null) Fijar(reticulos[i].gameObject, _reticuloVis, i, false);
            if (puntas != null && i < puntas.Length && puntas[i] != null) Fijar(puntas[i].gameObject, _puntaVis, i, false);
        }
    }
}
