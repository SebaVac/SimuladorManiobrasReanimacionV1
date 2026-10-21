using UnityEngine;

/// <summary>
/// Marcador de escena que indica dónde debe quedar la CABEZA del jugador al empezar y hacia dónde debe mirar.
/// <see cref="AlineadorInicioJugador"/> mueve el rig para que la cabeza coincida con la posición XZ de este objeto
/// y su rumbo (yaw) con el de su eje azul. La altura (Y) no se usa: la da el suelo real (origen FloorLevel).
/// </summary>
public class PuntoInicioJugador : MonoBehaviour
{
    [Tooltip("Radio del disco dibujado en el Editor (solo visual).")]
    [SerializeField] private float radioGizmo = 0.3f;
    [Tooltip("Largo de la flecha de dirección dibujada en el Editor (solo visual).")]
    [SerializeField] private float largoFlecha = 0.6f;

    /// <summary>Posición XZ de destino de la cabeza (Y = 0).</summary>
    public Vector3 PosicionCabeza => new Vector3(transform.position.x, 0f, transform.position.z);

    /// <summary>Rumbo de destino de la cabeza, en grados alrededor del eje vertical (0 = mirar hacia +Z).</summary>
    public float YawGrados
    {
        get
        {
            Vector3 f = transform.forward; f.y = 0f;
            return f.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 p = new Vector3(transform.position.x, transform.position.y + 0.01f, transform.position.z);
        Vector3 f = transform.forward; f.y = 0f;
        f = f.sqrMagnitude < 1e-6f ? Vector3.forward : f.normalized;
        Vector3 d = Vector3.Cross(Vector3.up, f);

        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
        const int seg = 32;
        Vector3 prev = p + f * radioGizmo;
        for (int i = 1; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            Vector3 cur = p + (f * Mathf.Cos(a) + d * Mathf.Sin(a)) * radioGizmo;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        Vector3 punta = p + f * largoFlecha;
        Gizmos.DrawLine(p, punta);
        Gizmos.DrawLine(punta, punta - f * 0.12f + d * 0.08f);
        Gizmos.DrawLine(punta, punta - f * 0.12f - d * 0.08f);
    }
#endif
}
