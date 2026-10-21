using System;
using UnityEngine;

/// <summary>
/// Entrada activa (manos o controles Touch) para SceneInicio y SceneDiagnostico. Se detecta sola, sin ajuste
/// en el menú: si solo hay controles activos, controles; si solo hay manos, manos; con ambos presentes gana el
/// último que se movió o apretó (el que no se mueve no desplaza al otro). Al cambiar, muestra los modelos del
/// control y oculta las manos (o al revés).
///
/// Solo expone, ya calculado, lo que necesitan los consumidores (todos sin asignaciones por fotograma):
///   - <see cref="TryRayo"/> y <see cref="GatilloDown"/> → GestorSeleccionToque (selección por rayo),
///   - <see cref="TryPuntaVirtual"/> → InteraccionPaciente (examen físico con punta a
///     <see cref="distanciaPuntaVirtual"/> delante del control),
///   - <see cref="Modo"/> → visuales (rayo, retículo, esfera de punta).
/// La lectura del dispositivo vive detrás de <see cref="ILecturaControles"/> para poder inyectarla en pruebas.
/// Sin vibración (la retroalimentación háptica queda fuera de alcance, informe 7.6).
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class EntradaControlesTouch : MonoBehaviour
{
    [Header("Rig (OVRCameraRig)")]
    [SerializeField] private Transform anclaControlIzquierdo;
    [SerializeField] private Transform anclaControlDerecho;
    [SerializeField] private OVRHand manoIzquierda;
    [SerializeField] private OVRHand manoDerecha;

    [Header("Punta virtual (examen físico)")]
    [Tooltip("Distancia (m) delante del control a la que se coloca la punta que alimenta la MISMA detección de " +
             "proximidad que las manos (mismo radio, cooldown y registro).")]
    [SerializeField] private float distanciaPuntaVirtual = 0.10f;

    [Header("Detección de actividad (entrada activa)")]
    [Tooltip("Desplazamiento mínimo por fotograma (m) para considerar que el control se movió.")]
    [SerializeField] private float umbralMovimientoControl = 0.003f;
    [Tooltip("Desplazamiento mínimo por fotograma (m) para considerar que la mano se movió.")]
    [SerializeField] private float umbralMovimientoMano = 0.004f;

    [Header("Visuales que se conmutan")]
    [Tooltip("Modelos de los controles (OVRControllerPrefab): visibles solo con controles activos.")]
    [SerializeField] private GameObject[] modelosControl;
    [Tooltip("Componentes que dibujan las manos (OVRMeshRenderer / OVRSkeletonRenderer): se apagan con controles activos.")]
    [SerializeField] private Behaviour[] renderersMano;
    [SerializeField] private Renderer[] mallasMano;

    /// <summary>Lectura del dispositivo. Por defecto OVR; las pruebas inyectan una sintética.</summary>
    public ILecturaControles Lectura { get; set; }

    public ModoEntrada Modo { get; private set; } = ModoEntrada.Manos;
    public event Action<ModoEntrada> AlCambiarModo;

    private readonly bool[] _ctrlPres = new bool[2];
    private readonly bool[] _manoPres = new bool[2];
    private readonly bool[] _poseValida = new bool[2];
    private readonly Vector3[] _origen = new Vector3[2];
    private readonly Vector3[] _dir = new Vector3[2];
    private readonly Vector3[] _origenPrev = new Vector3[2];
    private readonly Vector3[] _dirPrev = new Vector3[2];
    private readonly Vector3[] _manoPosPrev = new Vector3[2];
    private readonly bool[] _manoPosPrevValida = new bool[2];
    private readonly bool[] _gatilloPrev = new bool[2];
    private readonly bool[] _gatilloDown = new bool[2];

    private bool _usoManos, _usoControles;

    /// <summary>Dispositivo usado desde el último <see cref="ReiniciarRegistro"/> (Manos si no hubo uso).</summary>
    public DispositivoEntrada Dispositivo =>
        _usoManos && _usoControles ? DispositivoEntrada.Mixto
        : _usoControles ? DispositivoEntrada.Controles : DispositivoEntrada.Manos;

    void Awake()
    {
        if (Lectura == null) Lectura = new LecturaControlesOVR(anclaControlIzquierdo, anclaControlDerecho, manoIzquierda, manoDerecha);
    }

    void OnEnable() => AplicarVisuales(Modo);

    void Update()
    {
        var l = Lectura;
        if (l == null) return;

        bool actCtrl = false, actMano = false;
        for (int i = 0; i < 2; i++)
        {
            _ctrlPres[i] = l.ControlPresente(i);
            _manoPres[i] = l.ManoPresente(i);

            bool g = _ctrlPres[i] && l.GatilloPulsado(i);
            _gatilloDown[i] = g && !_gatilloPrev[i];
            if (g != _gatilloPrev[i]) actCtrl = true;
            _gatilloPrev[i] = g;

            _poseValida[i] = _ctrlPres[i] && l.LeerPoseControl(i, out _origen[i], out _dir[i]);
            if (_poseValida[i])
            {
                float m = _dir[i].sqrMagnitude;
                _dir[i] = m > 1e-8f ? _dir[i] / Mathf.Sqrt(m) : Vector3.forward;
                if ((_origen[i] - _origenPrev[i]).sqrMagnitude > umbralMovimientoControl * umbralMovimientoControl
                    || Vector3.Dot(_dir[i], _dirPrev[i]) < 0.99998f) // aprox. 0,36 grados
                    actCtrl = true;
                _origenPrev[i] = _origen[i];
                _dirPrev[i] = _dir[i];
            }

            if (l.LeerPosicionMano(i, out Vector3 pm))
            {
                if (_manoPosPrevValida[i] && (pm - _manoPosPrev[i]).sqrMagnitude > umbralMovimientoMano * umbralMovimientoMano)
                    actMano = true;
                _manoPosPrev[i] = pm;
                _manoPosPrevValida[i] = true;
            }
            else _manoPosPrevValida[i] = false;
        }

        bool hayC = _ctrlPres[0] || _ctrlPres[1];
        bool hayM = _manoPres[0] || _manoPres[1];
        ModoEntrada nuevo = Modo;
        if (hayC && !hayM) nuevo = ModoEntrada.Controles;
        else if (hayM && !hayC) nuevo = ModoEntrada.Manos;
        else if (hayC && hayM)
        {
            if (actCtrl && !actMano) nuevo = ModoEntrada.Controles;       // último que se movió o apretó
            else if (actMano && !actCtrl) nuevo = ModoEntrada.Manos;
        }

        if (nuevo != Modo)
        {
            Modo = nuevo;
            AplicarVisuales(nuevo);
            AlCambiarModo?.Invoke(nuevo);
        }
    }

    /// <summary>Rayo de apuntado del control de esa mano. Solo con controles como entrada activa.</summary>
    public bool TryRayo(int mano, out Vector3 origen, out Vector3 direccion)
    {
        bool ok = Modo == ModoEntrada.Controles && _poseValida[mano];
        origen = ok ? _origen[mano] : default;
        direccion = ok ? _dir[mano] : default;
        return ok;
    }

    /// <summary>Flanco de subida del gatillo en ESTE fotograma.</summary>
    public bool GatilloDown(int mano) => Modo == ModoEntrada.Controles && _gatilloDown[mano];

    /// <summary>Punta virtual: <see cref="distanciaPuntaVirtual"/> delante del control.</summary>
    public bool TryPuntaVirtual(int mano, out Vector3 punta)
    {
        if (!TryRayo(mano, out Vector3 o, out Vector3 d)) { punta = default; return false; }
        punta = o + d * distanciaPuntaVirtual;
        return true;
    }

    public float DistanciaPuntaVirtual => distanciaPuntaVirtual;

    /// <summary>Anota que el jugador interactuó con la entrada activa (selección o región examinada).</summary>
    public void RegistrarUso()
    {
        if (Modo == ModoEntrada.Controles) _usoControles = true; else _usoManos = true;
    }

    public void ReiniciarRegistro() { _usoManos = false; _usoControles = false; }

    private void AplicarVisuales(ModoEntrada modo)
    {
        bool controles = modo == ModoEntrada.Controles;
        if (modelosControl != null)
            for (int i = 0; i < modelosControl.Length; i++)
                if (modelosControl[i] != null && modelosControl[i].activeSelf != controles) modelosControl[i].SetActive(controles);

        // Manos: con controles se apagan los dibujos (OVRMeshRenderer reevalúa su malla cada Update, así
        // que se apaga primero el componente y luego la malla); con manos se reactivan.
        if (renderersMano != null)
            for (int i = 0; i < renderersMano.Length; i++) if (renderersMano[i] != null) renderersMano[i].enabled = !controles;
        if (mallasMano != null && controles)
            for (int i = 0; i < mallasMano.Length; i++) if (mallasMano[i] != null) mallasMano[i].enabled = false;
    }
}
