using UnityEngine;

/// <summary>
/// Posición fija de uno o varios paneles de SceneDiagnostico (hijos del root "PanelesFijosDiagnostico"). Los paneles
/// de ese slot son HIJOS de este objeto (su raíz con pose local fija), así que se ven donde esté el slot sin ningún
/// código en tiempo de ejecución: activar o desactivar la raíz del panel (por fase o por la pausa) no la mueve.
/// No tiene Update. El gizmo dibuja el rectángulo del panel más grande del slot y una flecha hacia el lado desde el
/// que se lee (el +Z del Canvas mira hacia el lado contrario al lector).
///
/// Para ajustar un slot: selecciónalo y arrástralo o gíralo en el Editor (el yaw gira el panel; no lo inclines). La altura
/// del centro es la Y del slot. Si reconstruyes un panel con su builder, vuelve a ejecutar
/// Tools/Razonamiento Clínico/Configurar paneles fijos (SceneDiagnostico): conserva la pose de los slots.
/// </summary>
public class SlotPanel : MonoBehaviour
{
    [Tooltip("Qué paneles van en este slot y en qué fases aparecen (solo informativo).")]
    [TextArea(2, 4)]
    [SerializeField] private string descripcion;

    [Tooltip("Tamaño en metros del panel más grande de este slot (solo para el gizmo del Editor).")]
    [SerializeField] private Vector2 tamano = new Vector2(0.7f, 0.7f);

    [Tooltip("Color del gizmo en el Editor.")]
    [SerializeField] private Color colorGizmo = new Color(0.2f, 0.9f, 1f, 1f);

    public string Descripcion => descripcion;
    public Vector2 Tamano => tamano;

    /// <summary>Configura el slot (lo usa el builder del Editor).</summary>
    public void Configurar(string desc, Vector2 tam, Color color)
    {
        descripcion = desc; tamano = tam; colorGizmo = color;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = colorGizmo;
        Gizmos.matrix = transform.localToWorldMatrix;
        float w = tamano.x / 2f, h = tamano.y / 2f;
        Vector3 a = new Vector3(-w, -h, 0f), b = new Vector3(w, -h, 0f), c = new Vector3(w, h, 0f), d = new Vector3(-w, h, 0f);
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(a, c); Gizmos.DrawLine(b, d);                                        // diagonales: marcan el centro
        // Flecha hacia el lector (-Z local): ahí debe estar la cabeza del jugador
        Vector3 punta = new Vector3(0f, 0f, -0.35f);
        Gizmos.DrawLine(Vector3.zero, punta);
        Gizmos.DrawLine(punta, punta + new Vector3(0.07f, 0f, 0.1f));
        Gizmos.DrawLine(punta, punta + new Vector3(-0.07f, 0f, 0.1f));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
