using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Árbitro central de la selección por TOQUE (punta del índice) para toda la
/// UI diegética del proyecto — SceneInicio incluido, que originalmente usaba
/// su propia implementación (GestorMenuPinch, ya eliminada) y hoy comparte
/// este mismo componente:
///   - con REGISTRO de <see cref="SeleccionableToque"/> (poblado en OnEnable/
///     OnDisable) en vez de <c>GetComponentsInChildren</c> por frame → cero-alloc;
///   - con ARBITRAJE: como máximo una selección por frame en todo el sistema;
///   - con CAPAS (<see cref="CapaActiva"/>): permite congelar la interacción
///     de la escena mientras un menú modal (GestorMenuPausa) está abierto.
///
/// Único <c>Update()</c>:
///   1. Punta del índice de cada mano (hueso Hand_IndexTip, cacheado).
///   2. Para cada selectable, <see cref="SeleccionableToque.PuntoDentro"/>
///      (dentro del rectángulo + tolerancia de profundidad).
///   3. Flanco de ENTRADA (el selectable tocado cambió respecto al frame
///      anterior) → selección. Sin gesto de pinch.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class GestorSeleccionToque : MonoBehaviour
{
    [Header("Manos (seguimiento nativo)")]
    [SerializeField] private OVRSkeleton esqueletoIzquierdo;
    [SerializeField] private OVRSkeleton esqueletoDerecho;

    [Header("Controles Touch (opcional)")]
    [Tooltip("Permite seleccionar con el rayo y el gatillo de los controles Touch cuando EntradaControlesTouch los " +
             "detecta como entrada activa. false = comportamiento actual solo con manos (SceneSimulador).")]
    [SerializeField] private bool permitirControles = false;
    [SerializeField] private EntradaControlesTouch entradaControles;
    [Tooltip("Alcance máximo del rayo (m): el visual llega hasta el punto de impacto o hasta esta distancia.")]
    [SerializeField] private float alcanceRayo = 1.5f;

    [Header("Selección")]
    [Tooltip("Ventana de gracia tras habilitarse (el árbitro al cargar la escena, o " +
             "cada SeleccionableToque cuando su panel aparece). Durante ese tiempo un " +
             "elemento que el dedo ya está tocando NO se selecciona — hay que sacar el " +
             "dedo y volver a entrar. Evita el auto-toque cuando un panel aparece bajo el dedo.")]
    [SerializeField] private float graciaInicioSeg = 0.4f;

    [Header("Depuración")]
    [SerializeField] private bool logSelecciones = false;

    /// <summary>Instancia única. Los <see cref="SeleccionableToque"/> se registran aquí.</summary>
    public static GestorSeleccionToque Instancia { get; private set; }

    /// <summary>
    /// Capa evaluada este frame. Un <see cref="SeleccionableToque"/> con
    /// <see cref="SeleccionableToque.Capa"/> distinto queda excluido (contacto
    /// forzado a false, no puede seleccionarse) sin que nadie tenga que
    /// deshabilitarlo uno por uno. La usa <c>GestorMenuPausa</c> para congelar
    /// el contenido de la escena (capa 0) mientras su panel (capa 1) o la
    /// confirmación (capa 2) están abiertos.
    /// </summary>
    public int CapaActiva { get; private set; } = 0;

    public void EstablecerCapa(int capa) => CapaActiva = capa;

    /// <summary>Se dispara tras cada selección confirmada.</summary>
    public event Action<SeleccionableToque> OnSeleccionEmitida;

    private readonly List<SeleccionableToque> _registro = new List<SeleccionableToque>(32);

    // Cache de la punta de índice por mano (huesos estables tras IsInitialized).
    private Transform _puntaIzq;
    private Transform _puntaDer;

    // Selectable que cada mano tocaba el frame anterior (para el flanco de entrada).
    private SeleccionableToque _tocadoIzqAntes;
    private SeleccionableToque _tocadoDerAntes;

    private float _tiempoHabilitado;

    // Rayos de los controles (índice 0 = izquierdo, 1 = derecho). Buffers fijos: sin asignaciones por fotograma.
    private readonly bool[] _rayoHay = new bool[2];
    private readonly Vector3[] _rayoOrigen = new Vector3[2];
    private readonly Vector3[] _rayoDir = new Vector3[2];
    private readonly Vector3[] _rayoFin = new Vector3[2];
    private readonly float[] _rayoDist = new float[2];
    private readonly SeleccionableToque[] _apuntado = new SeleccionableToque[2];
    private bool _modoControlesPrevio;

    /// <summary>true si esta escena admite controles y están configurados.</summary>
    public bool ControlesPermitidos => permitirControles && entradaControles != null;

    /// <summary>Rayo vigente de un control, para dibujarlo (línea hasta el impacto o el alcance + retículo).
    /// false si ese control no está apuntando (o la entrada activa son las manos).</summary>
    public bool ObtenerRayoVisual(int mano, out Vector3 origen, out Vector3 fin, out bool impacta)
    {
        bool ok = _modoControlesPrevio && _rayoHay[mano];
        origen = ok ? _rayoOrigen[mano] : default;
        fin = ok ? _rayoFin[mano] : default;
        impacta = ok && _apuntado[mano] != null;
        return ok;
    }

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Debug.LogWarning("[GestorSeleccionToque] Ya existe una instancia; se destruye la duplicada.", this);
            Destroy(this);
            return;
        }
        Instancia = this;
    }

    void OnEnable()
    {
        if (Instancia == null) Instancia = this;
        _tiempoHabilitado = Time.time;
        _tocadoIzqAntes = null;
        _tocadoDerAntes = null;

        var existentes = FindObjectsByType<SeleccionableToque>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < existentes.Length; i++) Registrar(existentes[i]);
    }

    void OnDisable()
    {
        for (int i = 0; i < _registro.Count; i++)
            if (_registro[i] != null) _registro[i].FijarContacto(false);
        _tocadoIzqAntes = null;
        _tocadoDerAntes = null;
        LimpiarRayos();
        _modoControlesPrevio = false;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // ── Registro ─────────────────────────────────────────────────────────────

    public void Registrar(SeleccionableToque s)
    {
        if (s == null || _registro.Contains(s)) return;
        _registro.Add(s);
    }

    public void Desregistrar(SeleccionableToque s) => _registro.Remove(s);

    // ── Bucle principal ──────────────────────────────────────────────────────

    void Update()
    {
        // Controles Touch como entrada activa: selección por rayo + gatillo. El camino de manos de abajo
        // queda intacto y es el único que corre cuando la entrada activa son las manos.
        if (permitirControles && entradaControles != null && entradaControles.Modo == ModoEntrada.Controles)
        {
            if (!_modoControlesPrevio)
            {
                LimpiarContactos();
                _modoControlesPrevio = true;
            }
            ActualizarRayos();
            return;
        }
        if (_modoControlesPrevio)
        {
            // Vuelta a las manos: sin rayos, sin contactos heredados y con ventana de gracia para que un dedo
            // que ya esté sobre un elemento no lo seleccione al reaparecer.
            LimpiarContactos();
            LimpiarRayos();
            _modoControlesPrevio = false;
            _tocadoIzqAntes = null;
            _tocadoDerAntes = null;
            _tiempoHabilitado = Time.time;
        }

        bool hayIzq = ObtenerPuntaIndice(esqueletoIzquierdo, ref _puntaIzq, out Vector3 pIzq);
        bool hayDer = ObtenerPuntaIndice(esqueletoDerecho,   ref _puntaDer, out Vector3 pDer);

        SeleccionableToque tocadoIzq = null;
        SeleccionableToque tocadoDer = null;

        for (int i = _registro.Count - 1; i >= 0; i--)
        {
            var s = _registro[i];
            if (s == null) { _registro.RemoveAt(i); continue; }

            if (!s.isActiveAndEnabled || !s.Interactuable || s.Capa != CapaActiva)
            {
                s.FijarContacto(false);
                continue;
            }

            bool cIzq = hayIzq && s.PuntoDentro(pIzq);
            bool cDer = hayDer && s.PuntoDentro(pDer);

            s.FijarContacto(cIzq || cDer);

            // Los rectángulos de los selectables no se solapan → basta el primer match.
            if (cIzq && tocadoIzq == null) tocadoIzq = s;
            if (cDer && tocadoDer == null) tocadoDer = s;
        }

        // Gracia inicial: durante los primeros ms tras activarse, se registra el
        // contacto actual pero NO se emite — así un elemento que aparece bajo el
        // dedo (panel head-locked, cambio de escena) no se auto-selecciona; hay
        // que sacar el dedo y volver a entrar.
        if (Time.time - _tiempoHabilitado < graciaInicioSeg)
        {
            _tocadoIzqAntes = tocadoIzq;
            _tocadoDerAntes = tocadoDer;
            return;
        }

        // Flanco de entrada por mano. Máx. una selección por frame en todo el sistema.
        // Un selectable recién habilitado (su panel apareció bajo el dedo) no se
        // emite durante su propia gracia — hay que sacar el dedo y volver a entrar.
        bool emitido = false;
        if (tocadoIzq != null && tocadoIzq != _tocadoIzqAntes && !tocadoIzq.EnGraciaInicio(graciaInicioSeg))
            emitido = Emitir(tocadoIzq);
        if (!emitido && tocadoDer != null && tocadoDer != _tocadoDerAntes && !tocadoDer.EnGraciaInicio(graciaInicioSeg))
            Emitir(tocadoDer);

        _tocadoIzqAntes = tocadoIzq;
        _tocadoDerAntes = tocadoDer;
    }

    // ── Controles Touch: rayo + gatillo ─────────────────────────────────────────

    private void LimpiarContactos()
    {
        for (int i = 0; i < _registro.Count; i++)
            if (_registro[i] != null) _registro[i].FijarContacto(false);
    }

    private void LimpiarRayos()
    {
        for (int i = 0; i < 2; i++) { _rayoHay[i] = false; _apuntado[i] = null; }
    }

    /// <summary>Un rayo por control. El elemento MÁS CERCANO que cada rayo intersecta (entre los activos, con
    /// Interactuable y de la capa vigente) recibe el hover; el flanco de subida del gatillo lo selecciona.
    /// Respeta ventana de gracia, cooldown y el límite de UNA selección por fotograma en todo el sistema.</summary>
    private void ActualizarRayos()
    {
        for (int i = 0; i < 2; i++)
        {
            _apuntado[i] = null;
            _rayoDist[i] = alcanceRayo;
            _rayoHay[i] = entradaControles.TryRayo(i, out _rayoOrigen[i], out _rayoDir[i]);
        }

        for (int j = _registro.Count - 1; j >= 0; j--)
        {
            var s = _registro[j];
            if (s == null) { _registro.RemoveAt(j); continue; }
            if (!s.isActiveAndEnabled || !s.Interactuable || s.Capa != CapaActiva) continue;

            for (int i = 0; i < 2; i++)
            {
                if (!_rayoHay[i]) continue;
                if (s.IntersectaRayo(_rayoOrigen[i], _rayoDir[i], _rayoDist[i], out float t) && t < _rayoDist[i])
                {
                    _rayoDist[i] = t;
                    _apuntado[i] = s;
                }
            }
        }

        for (int j = 0; j < _registro.Count; j++)
        {
            var s = _registro[j];
            if (s != null) s.FijarContacto(s == _apuntado[0] || s == _apuntado[1]);
        }

        for (int i = 0; i < 2; i++)
            if (_rayoHay[i]) _rayoFin[i] = _rayoOrigen[i] + _rayoDir[i] * _rayoDist[i];

        if (Time.time - _tiempoHabilitado < graciaInicioSeg) return;

        for (int i = 0; i < 2; i++)
        {
            var s = _apuntado[i];
            if (s != null && entradaControles.GatilloDown(i) && !s.EnGraciaInicio(graciaInicioSeg) && Emitir(s))
                break; // máx. una selección por fotograma
        }
    }

    /// <returns>true si la selección se emitió realmente (no estaba en cooldown).</returns>
    private bool Emitir(SeleccionableToque s)
    {
        if (!s.PuedeSeleccionar) return false;
        s.NotificarSeleccion();
        if (entradaControles != null) entradaControles.RegistrarUso();
        if (logSelecciones) Debug.Log($"[GestorSeleccionToque] Selección: {s.name}", s);
        OnSeleccionEmitida?.Invoke(s);
        return true;
    }

    // ── Helper OVR ───────────────────────────────────────────────────────────

    private static bool ObtenerPuntaIndice(OVRSkeleton esqueleto, ref Transform cache, out Vector3 pos)
    {
        pos = default;
        if (esqueleto == null || !esqueleto.IsInitialized || !esqueleto.IsDataValid) return false;

        if (cache == null) // primera vez, o el hueso fue recreado tras re-init del esqueleto
        {
            var huesos = esqueleto.Bones;
            if (huesos == null) return false;
            for (int i = 0; i < huesos.Count; i++)
            {
                if (huesos[i].Id == OVRSkeleton.BoneId.Hand_IndexTip)
                {
                    cache = huesos[i].Transform;
                    break;
                }
            }
            if (cache == null) return false;
        }

        pos = cache.position;
        return true;
    }
}
