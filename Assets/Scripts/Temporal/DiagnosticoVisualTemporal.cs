using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// TEMPORAL — herramienta de diagnóstico de las "formas fantasma" vistas en el Quest (SceneDiagnostico y
/// SceneSimulador). NO es parte del producto: vive bajo el root "DEBUG_VISUAL_TEMPORAL" y se quita con el menú
/// Tools/Temporal/Quitar DEBUG_VISUAL_TEMPORAL.
///
/// Un ítem del menú de pausa ("Diag visual: siguiente (n/N)") recorre estados. Cada estado cambia UNA sola cosa
/// respecto al estado 0 (normal) y SOLO en memoria, en ejecución: nada se guarda en assets ni en la escena. Al pasar
/// al siguiente estado (o al desactivarse el componente) se restaura todo lo tocado, de modo que el estado 0 es
/// siempre el original. Un estado que no se puede aplicar en esa escena/build se omite y se avisa en el log.
///
/// Trazas con el prefijo [DIAGVIS] (visibles en logcat). Cero asignaciones por fotograma: no hay Update; todo el
/// trabajo ocurre al cambiar de estado.
/// </summary>
public class DiagnosticoVisualTemporal : MonoBehaviour, IEtiquetaDinamica, IItemMenuCondicional
{
    public const int ULTIMO_ESTADO = 18;

    private static readonly string[] NOMBRES =
    {
        "normal",                                   // 0
        "techo oculto",                             // 1
        "ambientacion y esquinas ocultas",          // 2
        "ventana y puerta ocultas",                 // 3
        "manos: solo OVRMeshRenderer oculto",       // 4
        "manos: solo OVRSkeletonRenderer y capsulas ocultos", // 5
        "manos: objetos Detached ocultos",          // 6
        "modelos de control ocultos",               // 7
        "manos: todos los visuales ocultos",        // 8
        "paciente hologr. oculto",                  // 9
        "reflejos y brillos fuera (entorno)",       // 10
        "MSAA 1",                                   // 11
        "renderer features y Global Volume off",    // 12
        "foveated rendering off",                   // 13
        "Application SpaceWarp off",                // 14
        "near 0.1 / far 30",                        // 15
        "luces en tiempo real apagadas",            // 16
        "sala vieja ON, sala nueva y ambient. off", // 17
        "todo el entorno oculto",                   // 18
    };

    [Header("Referencias de la escena (las pone Tools/Temporal/Instalar)")]
    [SerializeField] private Transform raizEntorno;
    [SerializeField] private Transform raizAmbientacion;
    [Tooltip("Solo SceneSimulador: la sala vieja (desactivada).")]
    [SerializeField] private GameObject salaVieja;
    [SerializeField] private Transform raizPaciente;
    [Tooltip("Asset del renderer URP en uso (para apagar sus renderer features solo en memoria).")]
    [SerializeField] private UniversalRendererData rendererData;
    [Tooltip("Solo SceneSimulador: el Global Volume.")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private TextMeshPro texto;

    [Header("Menú de pausa")]
    [Tooltip("RectTransform del panel de pausa; se agranda en ejecución para que quepan los ítems temporales.")]
    [SerializeField] private RectTransform panelPausa;
    [SerializeField] private int itemsEnMenu = 1;
    [SerializeField] private float alturaExtraPorItem = 74f;

    private int _estado;
    private string _etiqueta;
    private readonly bool[] _disponible = new bool[ULTIMO_ESTADO + 1];
    private readonly StringBuilder _sb = new StringBuilder(768);
    private bool _iniciado;

    // Registro de lo tocado para restaurarlo
    private readonly List<Renderer> _rend = new List<Renderer>();
    private readonly List<Behaviour> _beh = new List<Behaviour>();
    private readonly List<Renderer> _matRend = new List<Renderer>();
    private readonly List<Material[]> _matPrev = new List<Material[]>();
    private readonly Dictionary<Material, Material> _copias = new Dictionary<Material, Material>();
    private readonly List<ScriptableRendererFeature> _feat = new List<ScriptableRendererFeature>();
    private readonly List<bool> _featPrev = new List<bool>();
    private readonly List<Renderer> _tmpRend = new List<Renderer>();
    private int _msaaPrev = -1;
    private bool _ffrTocado;
    private OVRManager.FoveatedRenderingLevel _ffrPrev;
    private bool _ffrDinPrev;
    private bool _swTocado;
    private Camera _camara;
    private float _farPrev = -1f;
    private float _nearPrev = -1f;
    private bool _salaViejaActivada;

    // Detectados al iniciar
    private EntradaControlesTouch _entrada;
    private OVRCameraRig _rig;
    private int _nDetached;
    private int _nControles;

    // ── Menú de pausa ───────────────────────────────────────────────────────────────────────────────

    public string ObtenerEtiqueta() => _etiqueta;
    public bool DebeMostrarseEnMenu() => true;

    /// <summary>Acción del ítem del menú de pausa.</summary>
    public void Siguiente()
    {
        Iniciar();
        int n = _estado;
        Restaurar();
        do { n = (n + 1) % (ULTIMO_ESTADO + 1); } while (!_disponible[n]);
        Aplicar(n);
    }

    // ── Ciclo de vida ───────────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Iniciar();
        if (panelPausa != null && itemsEnMenu > 0)
            panelPausa.sizeDelta += new Vector2(0f, itemsEnMenu * alturaExtraPorItem);
    }

    void Start()
    {
        if (texto != null && _rig != null && _rig.centerEyeAnchor != null)
        {
            texto.transform.SetParent(_rig.centerEyeAnchor, false);
            texto.transform.localPosition = new Vector3(0f, -0.26f, 0.7f);
            texto.transform.localRotation = Quaternion.identity;
            ActualizarTexto();
        }
    }

    void OnDisable() { if (_iniciado) Restaurar(); }
    void OnDestroy()
    {
        if (_entrada != null) _entrada.AlCambiarModo -= AlCambiarModoEntrada;
        Restaurar();
        LimpiarCopias();
    }

    /// <summary>EntradaControlesTouch.AplicarVisuales reactiva OVRMeshRenderer y OVRSkeletonRenderer al volver a modo manos
    /// (y apaga la malla al pasar a controles). El evento se dispara DESPUÉS de AplicarVisuales: se vuelve a ocultar todo lo
    /// que el estado activo había ocultado, para que los estados 4, 5 y 8 se mantengan al alternar manos y controles.</summary>
    private void AlCambiarModoEntrada(ModoEntrada modo)
    {
        int n = 0;
        for (int i = 0; i < _beh.Count; i++) if (_beh[i] != null && _beh[i].enabled) { _beh[i].enabled = false; n++; }
        for (int i = 0; i < _rend.Count; i++) if (_rend[i] != null && _rend[i].enabled) { _rend[i].enabled = false; n++; }
        if (n == 0) return;
        _sb.Length = 0;
        _sb.Append("[DIAGVIS] modo de entrada -> ").Append(modo).Append(": EntradaControlesTouch reactivó ").Append(n)
           .Append(" pieza(s) del estado ").Append(_estado).Append("; se vuelven a ocultar");
        Debug.Log(_sb.ToString(), this);
    }

    // Al volver al estado 0 con controles activos, la malla y el esqueleto de la mano deben seguir apagados (así los deja
    // EntradaControlesTouch en ese modo); se reactivarán solos cuando el modo vuelva a manos.
    private bool ManoApagadaPorControles(Component c)
    {
        if (_entrada == null || _entrada.Modo != ModoEntrada.Controles) return false;
        return c is OVRMeshRenderer || c is OVRSkeletonRenderer || (c is SkinnedMeshRenderer && c.GetComponent<OVRMeshRenderer>() != null);
    }

    private void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        _rig = FindFirstObjectByType<OVRCameraRig>();
        _entrada = FindFirstObjectByType<EntradaControlesTouch>();
        if (_entrada != null) _entrada.AlCambiarModo += AlCambiarModoEntrada;
        _camara = _rig != null && _rig.centerEyeAnchor != null ? _rig.centerEyeAnchor.GetComponent<Camera>() : Camera.main;

        _nDetached = 0;
        if (_rig != null)
        {
            ContarRenderers(_rig.leftHandAnchorDetached, ref _nDetached);
            ContarRenderers(_rig.rightHandAnchorDetached, ref _nDetached);
            var ctl = _rig.GetComponentsInChildren<OVRControllerHelper>(true);
            _nControles = ctl.Length;
        }

        var omitidos = new StringBuilder();
        for (int i = 0; i <= ULTIMO_ESTADO; i++)
        {
            string motivo = null;
            switch (i)
            {
                case 1: case 3: case 18: if (raizEntorno == null) motivo = "sin raizEntorno"; break;
                case 2: if (raizAmbientacion == null) motivo = "sin ambientacion"; break;
                case 4: case 5: case 8: if (_rig == null) motivo = "sin OVRCameraRig"; break;
                case 6: if (_nDetached == 0) motivo = "LeftHandAnchorDetached/RightHandAnchorDetached no tienen renderers"; break;
                case 7: if (_nControles == 0) motivo = "no hay modelos de control en esta escena"; break;
                case 9: if (raizPaciente == null) motivo = "sin paciente"; break;
                case 12: if (rendererData == null && globalVolume == null) motivo = "sin renderer features ni Global Volume"; break;
                case 15: if (_camara == null) motivo = "sin camara"; break;
                case 17: if (salaVieja == null) motivo = "esta escena no tiene sala vieja"; break;
            }
            _disponible[i] = motivo == null;
            if (motivo != null) omitidos.Append(" [").Append(i).Append(": ").Append(motivo).Append(']');
        }
        _estado = 0;
        _etiqueta = "Diag visual: siguiente (0/" + ULTIMO_ESTADO + ")";

        _sb.Length = 0;
        _sb.Append("[DIAGVIS] listo en ").Append(gameObject.scene.name).Append(" | estados 0..").Append(ULTIMO_ESTADO)
           .Append(" | omitidos:").Append(omitidos.Length == 0 ? " ninguno" : omitidos.ToString())
           .Append(" | URP asset ").Append(UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.name + " MSAA=" + UniversalRenderPipeline.asset.msaaSampleCount : "?")
           .Append(" | rendererData ").Append(rendererData != null ? rendererData.name : "ninguno")
           .Append(" | FFR nivel ").Append(OVRManager.foveatedRenderingLevel).Append(" soportado=").Append(OVRManager.fixedFoveatedRenderingSupported)
           .Append(" dinamico=").Append(OVRManager.useDynamicFoveatedRendering)
           .Append(" | SpaceWarp ").Append(OVRManager.GetSpaceWarp());
        Debug.Log(_sb.ToString(), this);
    }

    private static void ContarRenderers(Transform raiz, ref int n)
    {
        if (raiz != null) n += raiz.GetComponentsInChildren<Renderer>(true).Length;
    }

    // ── Aplicar / restaurar ─────────────────────────────────────────────────────────────────────────

    private void Aplicar(int id)
    {
        _estado = id;
        _etiqueta = "Diag visual: siguiente (" + id + "/" + ULTIMO_ESTADO + ")";
        _sb.Length = 0;
        string detalle = "";
        int nRend0 = _rend.Count, nBeh0 = _beh.Count;

        switch (id)
        {
            case 1: OcultarBajo(raizEntorno != null ? raizEntorno.Find("Techo") : null); break;
            case 2: OcultarBajo(raizAmbientacion); break;
            case 3: OcultarPorNombre(raizEntorno, "wall_window", "door_1", "wall_door"); break;
            case 4: OcultarManoMalla(); break;
            case 5: OcultarManoEsqueleto(); break;
            case 6:
                OcultarBajo(_rig.leftHandAnchorDetached); OcultarBajo(_rig.rightHandAnchorDetached); break;
            case 7:
                foreach (var h in _rig.GetComponentsInChildren<OVRControllerHelper>(true)) OcultarBajo(h.transform);
                break;
            case 8: OcultarManoMalla(); OcultarManoEsqueleto(); break;
            case 9: OcultarBajo(raizPaciente); break;
            case 10: detalle = ReflejosFuera(); break;
            case 11: detalle = Msaa1(); break;
            case 12: detalle = FeaturesYVolumen(); break;
            case 13: detalle = FoveacionOff(); break;
            case 14: detalle = SpaceWarpOff(); break;
            case 15: detalle = ClipCamara(); break;
            case 16: detalle = LucesOff(); break;
            case 17: detalle = SalaVieja(); break;
            case 18:
                OcultarBajo(raizEntorno); OcultarBajo(raizAmbientacion);
                break;
        }

        _sb.Append("[DIAGVIS] estado ").Append(id).Append('/').Append(ULTIMO_ESTADO).Append(" '").Append(NOMBRES[id]).Append("' | ")
           .Append("renderers ocultados=").Append(_rend.Count - nRend0).Append(" componentes desactivados=").Append(_beh.Count - nBeh0);
        if (detalle.Length > 0) _sb.Append(" | ").Append(detalle);
        Debug.Log(_sb.ToString(), this);
        ActualizarTexto();
    }

    /// <summary>Deja TODO como estaba en el estado 0.</summary>
    private void Restaurar()
    {
        if (_estado == 0 && _rend.Count == 0 && _beh.Count == 0 && _matRend.Count == 0 && _feat.Count == 0
            && _msaaPrev < 0 && !_ffrTocado && !_swTocado && _farPrev < 0f && !_salaViejaActivada) return;

        for (int i = _beh.Count - 1; i >= 0; i--) if (_beh[i] != null && !ManoApagadaPorControles(_beh[i])) _beh[i].enabled = true;
        for (int i = _rend.Count - 1; i >= 0; i--) if (_rend[i] != null && !ManoApagadaPorControles(_rend[i])) _rend[i].enabled = true;
        _beh.Clear(); _rend.Clear();

        for (int i = 0; i < _matRend.Count; i++) if (_matRend[i] != null) _matRend[i].sharedMaterials = _matPrev[i];
        _matRend.Clear(); _matPrev.Clear();
        LimpiarCopias();

        for (int i = 0; i < _feat.Count; i++) if (_feat[i] != null) _feat[i].SetActive(_featPrev[i]);
        _feat.Clear(); _featPrev.Clear();

        if (_msaaPrev >= 0 && UniversalRenderPipeline.asset != null) UniversalRenderPipeline.asset.msaaSampleCount = _msaaPrev;
        _msaaPrev = -1;

        if (_ffrTocado) { OVRManager.foveatedRenderingLevel = _ffrPrev; OVRManager.useDynamicFoveatedRendering = _ffrDinPrev; _ffrTocado = false; }
        if (_swTocado) { OVRManager.SetSpaceWarp(true); _swTocado = false; }

        if (_farPrev >= 0f && _camara != null) { _camara.farClipPlane = _farPrev; _camara.nearClipPlane = _nearPrev; }
        _farPrev = -1f; _nearPrev = -1f;

        if (_salaViejaActivada && salaVieja != null) salaVieja.SetActive(false);
        _salaViejaActivada = false;

        if (_estado != 0)
        {
            _sb.Length = 0;
            _sb.Append("[DIAGVIS] estado ").Append(_estado).Append(" restaurado a 0");
            Debug.Log(_sb.ToString(), this);
        }
        _estado = 0;
        _etiqueta = "Diag visual: siguiente (0/" + ULTIMO_ESTADO + ")";
        ActualizarTexto();
    }

    private void LimpiarCopias()
    {
        foreach (var kv in _copias) if (kv.Value != null) Destroy(kv.Value);
        _copias.Clear();
    }

    private void ActualizarTexto()
    {
        if (texto == null) return;
        texto.text = "DIAGVIS " + _estado + "/" + ULTIMO_ESTADO + "\n" + NOMBRES[_estado];
    }

    // ── Operaciones ─────────────────────────────────────────────────────────────────────────────────

    private void Ocultar(Renderer r)
    {
        if (r == null || !r.enabled) return;
        _rend.Add(r);
        r.enabled = false;
    }

    private void Desactivar(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        _beh.Add(b);
        b.enabled = false;
    }

    /// <summary>Oculta todos los renderers (activos o no) bajo la raíz.</summary>
    private void OcultarBajo(Transform raiz)
    {
        if (raiz == null) return;
        raiz.GetComponentsInChildren(true, _tmpRend);
        for (int i = 0; i < _tmpRend.Count; i++) Ocultar(_tmpRend[i]);
        _tmpRend.Clear();
    }

    private void OcultarPorNombre(Transform raiz, string a, string b, string c)
    {
        if (raiz == null) return;
        raiz.GetComponentsInChildren(true, _tmpRend);
        for (int i = 0; i < _tmpRend.Count; i++)
        {
            string n = _tmpRend[i].name;
            if (n == a || n == b || n == c) Ocultar(_tmpRend[i]);
        }
        _tmpRend.Clear();
    }

    // OVRMeshRenderer reevalúa SkinnedMeshRenderer.enabled en cada Update (por confianza del tracking), así que
    // se desactiva primero el componente y luego la malla. El tracking (OVRHand/OVRSkeleton) no se toca.
    private void OcultarManoMalla()
    {
        foreach (var h in _rig.GetComponentsInChildren<OVRHand>(true))
        {
            Desactivar(h.GetComponent<OVRMeshRenderer>());
            Ocultar(h.GetComponent<SkinnedMeshRenderer>());
        }
    }

    private void OcultarManoEsqueleto()
    {
        foreach (var h in _rig.GetComponentsInChildren<OVRHand>(true))
        {
            Desactivar(h.GetComponent<OVRSkeletonRenderer>());
            h.GetComponentsInChildren(true, _tmpRend);
            for (int i = 0; i < _tmpRend.Count; i++) if (!(_tmpRend[i] is SkinnedMeshRenderer)) Ocultar(_tmpRend[i]);
            _tmpRend.Clear();
        }
    }

    private bool EsDelEntorno(Transform t)
    {
        return (raizEntorno != null && t.IsChildOf(raizEntorno)) || (raizAmbientacion != null && t.IsChildOf(raizAmbientacion));
    }

    private string ReflejosFuera()
    {
        int nMat = 0, nRend = 0;
        for (int pasada = 0; pasada < 2; pasada++)
        {
            var raiz = pasada == 0 ? raizEntorno : raizAmbientacion;
            if (raiz == null) continue;
            raiz.GetComponentsInChildren(true, _tmpRend);
            for (int i = 0; i < _tmpRend.Count; i++)
            {
                var r = _tmpRend[i];
                var originales = r.sharedMaterials;
                Material[] nuevos = null;
                for (int k = 0; k < originales.Length; k++)
                {
                    var m = originales[k];
                    if (m == null || !m.HasProperty("_Smoothness")) continue;
                    if (!_copias.TryGetValue(m, out var copia))
                    {
                        copia = new Material(m) { name = m.name + "_DIAGVIS" };
                        copia.SetFloat("_Smoothness", 0f);
                        if (copia.HasProperty("_SpecularHighlights")) copia.SetFloat("_SpecularHighlights", 0f);
                        if (copia.HasProperty("_EnvironmentReflections")) copia.SetFloat("_EnvironmentReflections", 0f);
                        copia.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                        copia.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                        _copias[m] = copia; nMat++;
                    }
                    if (nuevos == null) nuevos = (Material[])originales.Clone();
                    nuevos[k] = copia;
                }
                if (nuevos != null) { _matRend.Add(r); _matPrev.Add(originales); r.sharedMaterials = nuevos; nRend++; }
            }
            _tmpRend.Clear();
        }
        return "materiales copiados=" + nMat + " renderers reasignados=" + nRend
             + " (Smoothness=0 siempre aplica; las keywords _SPECULARHIGHLIGHTS_OFF/_ENVIRONMENTREFLECTIONS_OFF solo hacen efecto si esas variantes del shader entraron al build)";
    }

    private string Msaa1()
    {
        var a = UniversalRenderPipeline.asset;
        if (a == null) return "sin URP asset";
        _msaaPrev = a.msaaSampleCount;
        a.msaaSampleCount = 1;
        return "MSAA " + _msaaPrev + " -> " + a.msaaSampleCount;
    }

    private string FeaturesYVolumen()
    {
        var s = new StringBuilder(160);
        int nf = 0;
        if (rendererData != null)
        {
            var fs = rendererData.rendererFeatures;
            for (int i = 0; i < fs.Count; i++)
            {
                if (fs[i] == null || !fs[i].isActive) continue;
                _feat.Add(fs[i]); _featPrev.Add(true); fs[i].SetActive(false); nf++;
                s.Append(fs[i].name).Append(' ');
            }
        }
        s.Insert(0, "features apagadas=" + nf + " (");
        s.Append(") | Global Volume ");
        if (globalVolume != null && globalVolume.enabled) { Desactivar(globalVolume); s.Append("desactivado"); }
        else s.Append(globalVolume == null ? "no existe en esta escena" : "ya estaba desactivado");
        return s.ToString();
    }

    private string FoveacionOff()
    {
        _ffrPrev = OVRManager.foveatedRenderingLevel;
        _ffrDinPrev = OVRManager.useDynamicFoveatedRendering;
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Off;
        OVRManager.useDynamicFoveatedRendering = false;
        _ffrTocado = true;
        return "FFR " + _ffrPrev + " -> " + OVRManager.foveatedRenderingLevel + " | dinamico " + _ffrDinPrev + " -> " + OVRManager.useDynamicFoveatedRendering
             + " | soportado=" + OVRManager.fixedFoveatedRenderingSupported;
    }

    private string SpaceWarpOff()
    {
        bool activo = OVRManager.GetSpaceWarp();
        if (!activo) return "Application SpaceWarp ya estaba desactivado: este estado no cambia nada";
        OVRManager.SetSpaceWarp(false);
        _swTocado = true;
        return "Application SpaceWarp true -> " + OVRManager.GetSpaceWarp();
    }

    private string ClipCamara()
    {
        if (_camara == null) return "sin camara";
        _nearPrev = _camara.nearClipPlane; _farPrev = _camara.farClipPlane;
        _camara.nearClipPlane = 0.1f; _camara.farClipPlane = 30f;
        return "near " + _nearPrev.ToString("0.###") + " -> 0.1 | far " + _farPrev.ToString("0.###") + " -> 30 (camara " + _camara.name + ")";
    }

    private string LucesOff()
    {
        int n = 0;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!l.enabled) continue;
            Desactivar(l); n++;
        }
        return "luces apagadas=" + n;
    }

    private string SalaVieja()
    {
        var s = new StringBuilder(160);
        int nLuces = salaVieja.GetComponentsInChildren<Light>(true).Length;
        int nScripts = 0;
        foreach (var mb in salaVieja.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) nScripts++;
        salaVieja.SetActive(true); _salaViejaActivada = true;
        OcultarBajo(raizEntorno); OcultarBajo(raizAmbientacion);
        s.Append("sala vieja ON (luces=").Append(nLuces).Append(" scripts=").Append(nScripts).Append("), sala nueva y ambientacion ocultas (incluye Tablero de SuperficiePaciente)");
        return s.ToString();
    }
}
