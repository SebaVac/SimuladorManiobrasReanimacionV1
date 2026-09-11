using UnityEngine;

/// <summary>
/// Mantiene este transform a una distancia fija frente a la cámara
/// (CenterEyeAnchor), siguiendo la cabeza — para paneles que deben permanecer
/// siempre en el campo de visión (p. ej. el Panel de Introducción).
///
/// El menú principal del proyecto NO es head-locked (es un Canvas World Space
/// fijo en (0,0,1)); no había patrón previo que reutilizar, así que este
/// componente lo resuelve de forma explícita y reutilizable.
///
/// Colocar en el GameObject raíz del panel. Al activarse hace un "snap"
/// inmediato a la posición; luego sigue con suavizado configurable.
/// </summary>
[DisallowMultipleComponent]
public class AnclajeCabeza : MonoBehaviour
{
    [Tooltip("CenterEyeAnchor. Si queda vacío se usa Camera.main.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("Metros frente a la cámara.")]
    [SerializeField] private float distancia = 1.2f;

    [Tooltip("Offset vertical respecto a la línea de visión, en metros.")]
    [SerializeField] private float alturaRelativa = 0f;

    [Tooltip("Offset horizontal respecto al centro de la vista, en metros. Negativo = izquierda.")]
    [SerializeField] private float desplazamientoLateral = 0f;

    [Tooltip("0 = rígido (pegado a la cabeza). >0 = seguimiento suavizado (mayor = más rápido).")]
    [SerializeField] private float suavizado = 8f;

    [Tooltip("Ignora cabeceo/alabeo: el panel queda siempre a nivel y solo gira en yaw.")]
    [SerializeField] private bool soloYaw = true;

    private Transform _cam;

    void OnEnable()
    {
        ResolverCamara();
        if (_cam != null) Colocar(instantaneo: true);
    }

    void LateUpdate()
    {
        if (_cam == null) { ResolverCamara(); if (_cam == null) return; }
        Colocar(instantaneo: suavizado <= 0f);
    }

    private void ResolverCamara()
    {
        _cam = objetivo != null ? objetivo
             : (Camera.main != null ? Camera.main.transform : null);
    }

    private void Colocar(bool instantaneo)
    {
        Vector3 fwd = _cam.forward;
        if (soloYaw)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return; // mirando recto arriba/abajo
            fwd.Normalize();
        }

        Vector3 derecha = Vector3.Cross(Vector3.up, fwd).normalized;
        Vector3 posObjetivo = _cam.position
                            + fwd * distancia
                            + Vector3.up * alturaRelativa
                            + derecha * desplazamientoLateral;
        Quaternion rotObjetivo = Quaternion.LookRotation(fwd, Vector3.up);

        if (instantaneo)
        {
            transform.SetPositionAndRotation(posObjetivo, rotObjetivo);
        }
        else
        {
            float t = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, posObjetivo, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotObjetivo, t);
        }
    }
}
