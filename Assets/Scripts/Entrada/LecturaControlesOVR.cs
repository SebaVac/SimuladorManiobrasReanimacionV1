using UnityEngine;

/// <summary>
/// Lectura real de Meta XR (OVRInput + anclas del OVRCameraRig). Solo lee; no decide nada.
/// El origen/dirección del rayo salen de las anclas de control del rig (<c>LeftControllerAnchor</c> /
/// <c>RightControllerAnchor</c>, que OVRCameraRig actualiza con la pose del control): origen = posición del
/// ancla, dirección = su <c>forward</c>. Sin asignaciones por fotograma.
/// </summary>
public class LecturaControlesOVR : ILecturaControles
{
    private readonly Transform[] _anclasControl;
    private readonly OVRHand[] _manos;
    private const float UmbralGatillo = 0.55f;
    private readonly bool[] _gatilloEstado = new bool[2];

    public LecturaControlesOVR(Transform anclaControlIzq, Transform anclaControlDer, OVRHand manoIzq, OVRHand manoDer)
    {
        _anclasControl = new[] { anclaControlIzq, anclaControlDer };
        _manos = new[] { manoIzq, manoDer };
    }

    private static OVRInput.Controller Tipo(int mano) => mano == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

    public bool ControlPresente(int mano)
    {
        var c = Tipo(mano);
        return OVRInput.IsControllerConnected(c) && OVRInput.GetControllerPositionTracked(c);
    }

    public bool LeerPoseControl(int mano, out Vector3 origen, out Vector3 direccion)
    {
        var a = _anclasControl[mano];
        if (a == null) { origen = default; direccion = default; return false; }
        origen = a.position;
        direccion = a.forward;
        return true;
    }

    public bool GatilloPulsado(int mano)
    {
        // Histéresis (0,55 sube / 0,35 baja) para no rebotar con el gatillo analógico.
        float v = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, Tipo(mano));
        if (_gatilloEstado[mano]) { if (v < 0.35f) _gatilloEstado[mano] = false; }
        else if (v > UmbralGatillo) _gatilloEstado[mano] = true;
        return _gatilloEstado[mano];
    }

    public bool ManoPresente(int mano) => _manos[mano] != null && _manos[mano].IsTracked;

    public bool LeerPosicionMano(int mano, out Vector3 posicion)
    {
        if (_manos[mano] == null || !_manos[mano].IsTracked) { posicion = default; return false; }
        posicion = _manos[mano].transform.position;
        return true;
    }
}
