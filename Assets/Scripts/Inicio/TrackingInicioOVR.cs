using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Implementación real sobre Meta XR (SDK v81, Oculus XR Plugin). Los delegados se crean una sola vez.
///
/// CONDICIÓN DE "TRACKING VÁLIDO" (PoseCabezaValida), todas desde la API instalada:
///   1. OVRManager.isHmdPresent (OVRPlugin.hmdPresent = nodo EyeCenter presente);
///   2. OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter): es EXACTAMENTE la señal con la que OVRManager dispara
///      TrackingAcquired (OVRTracker.isPositionTracked -> OVRPlugin.positionTracked) y el nodo que OVRCameraRig usa para
///      mover CenterEyeAnchor;
///   3. CenterEyeAnchor ya tiene una pose distinta de la de defecto (OVRCameraRig actualiza las anclas en su Update: en el
///      Start de la escena todavía están en el origen).
/// La versión anterior exigía OVRPlugin.GetNodePositionValid(Node.Head), un nodo distinto del que mueve el ancla, y solo se
/// evaluaba una vez en Start. Además se esperaba el evento TrackingAcquired, que NO vuelve a dispararse al cargar otra
/// escena: OVRManager.wasPositionTracked es estático y se queda en true (OVRManager.cs, línea 2040).
/// "Visor puesto" (OVRPlugin.userPresent) solo se registra: no se exige porque no se puede comprobar aquí que sea fiable.
/// </summary>
public class TrackingInicioOVR : ITrackingInicio
{
    public event Action TrackingAdquirido;
    public event Action Recentrado;

    private readonly Action _alAdquirir;
    private readonly Action _alRecentrar;
    private OVRDisplay _displayEnlazado;
    private bool _conectado;
    private string _condicion = "ninguna";
    private static FieldInfo _campoOrigenPedido;
    private static bool _campoBuscado;

    public const string NOMBRE_CONDICION = "isHmdPresent + EyeCenter con posición rastreada + ancla con pose";

    public TrackingInicioOVR()
    {
        _alAdquirir = AlAdquirir;
        _alRecentrar = AlRecentrar;
    }

    public string CondicionCumplida => _condicion;

    public bool PoseCabezaValida(Transform cabeza)
    {
        if (cabeza == null || !OVRManager.isHmdPresent) return false;
        if (!OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)) return false;
        // OVRCameraRig solo mueve el ancla cuando recibe una pose válida: en el Start todavía está en el origen y sin rotar
        if (cabeza.localPosition.sqrMagnitude <= 1e-6f && cabeza.localRotation == Quaternion.identity) return false;
        _condicion = NOMBRE_CONDICION;
        return true;
    }

    public void Conectar()
    {
        if (_conectado) return;
        _conectado = true;
        OVRManager.TrackingAcquired += _alAdquirir;
        OVRManager.HMDAcquired += _alAdquirir;       // si el visor aparece tarde, reintenta enlazar el display
        EnlazarDisplay();
    }

    public void Desconectar()
    {
        if (!_conectado) return;
        _conectado = false;
        OVRManager.TrackingAcquired -= _alAdquirir;
        OVRManager.HMDAcquired -= _alAdquirir;
        if (_displayEnlazado != null) { _displayEnlazado.RecenteredPose -= _alRecentrar; _displayEnlazado = null; }
    }

    // OVRManager.trackingOriginType devuelve el origen EFECTIVO cuando hay visor: lo que se imprimía como "origen" ya era el efectivo.
    public string DescribirOrigen() =>
        OVRManager.instance != null ? OVRManager.instance.trackingOriginType.ToString() : "sin OVRManager (sin visor)";

    public string DescribirOrigenPedido()
    {
        if (OVRManager.instance == null) return "sin OVRManager";
        if (!_campoBuscado)
        {
            _campoBuscado = true;
            _campoOrigenPedido = typeof(OVRManager).GetField("_trackingOriginType", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        return _campoOrigenPedido != null ? _campoOrigenPedido.GetValue(OVRManager.instance).ToString() : "n/d";
    }

    public string DescribirOrigenEfectivo()
    {
        try { return OVRManager.isHmdPresent && OVRPlugin.initialized ? OVRPlugin.GetTrackingOriginType().ToString() : "n/d (sin visor)"; }
        catch (Exception) { return "n/d"; }
    }

    public bool TryAlturaNodo(out float alturaY)
    {
        alturaY = 0f;
        if (!OVRManager.isHmdPresent) return false;
        if (!OVRNodeStateProperties.GetNodeStatePropertyVector3(XRNode.CenterEye, NodeStatePropertyType.Position,
                OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render, out Vector3 pos)) return false;
        alturaY = pos.y;
        return true;
    }

    public string Diagnosticar(Transform cabeza)
    {
        bool hmd = OVRManager.isHmdPresent;
        string eye = "n/d", head = "n/d", usr = "n/d";
        try
        {
            if (hmd)
            {
                eye = OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter).ToString();
                head = OVRPlugin.GetNodePositionValid(OVRPlugin.Node.Head).ToString();
                usr = OVRPlugin.userPresent.ToString();
            }
        }
        catch (Exception) { }
        string ancla = cabeza == null ? "n/d" : (cabeza.localPosition.sqrMagnitude > 1e-6f || cabeza.localRotation != Quaternion.identity).ToString();
        return "isHmdPresent=" + hmd + ", EyeCenter posición rastreada=" + eye + ", Head posición válida=" + head + ", usuario presente=" + usr + ", ancla con pose=" + ancla;
    }

    private void EnlazarDisplay()
    {
        var d = OVRManager.display;
        if (d == null || d == _displayEnlazado) return;
        if (_displayEnlazado != null) _displayEnlazado.RecenteredPose -= _alRecentrar;
        _displayEnlazado = d;
        d.RecenteredPose += _alRecentrar;
    }

    private void AlAdquirir() { EnlazarDisplay(); TrackingAdquirido?.Invoke(); }
    private void AlRecentrar() => Recentrado?.Invoke();
}
