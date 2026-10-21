using UnityEngine;

/// <summary>
/// TEMPORAL — ítems de depuración del metrónomo en el menú de pausa de SceneSimulador (root DEBUG_VISUAL_TEMPORAL; se quitan
/// con Tools/Temporal/Quitar DEBUG_VISUAL_TEMPORAL). Un componente por ítem; <see cref="accion"/> elige cuál:
///  - Modo: alterna MetronomoRCP entre UnoPorTic y Programado.
///  - ClickPrueba: un PlayOneShot suelto sobre el AudioSource original, sin rejilla.
///  - DspBuffer: alterna el DSP buffer 256/512/1024 con AudioSettings.Reset. Reset detiene TODAS las fuentes (también el
///    zumbido ambiente) e invalida los clips procedurales; MetronomoRCP y AmbienteSonoroUrgencias se recuperan solos.
/// Cada acción escribe [METRO]. Sin Update: cero asignaciones por fotograma.
/// </summary>
public class DiagnosticoMetronomoTemporal : MonoBehaviour, IEtiquetaDinamica, IItemMenuCondicional
{
    public enum Accion { Modo, ClickPrueba, DspBuffer }

    [SerializeField] private Accion accion;
    [SerializeField] private MetronomoRCP metronomo;
    [SerializeField] private AmbienteSonoroUrgencias ambiente;

    [Header("Menú de pausa")]
    [Tooltip("RectTransform del panel de pausa. Solo el componente con 'itemsQueAmplia' > 0 lo agranda (en ejecución).")]
    [SerializeField] private RectTransform panelPausa;
    [SerializeField] private int itemsQueAmplia;
    [SerializeField] private float alturaExtraPorItem = 74f;

    public bool DebeMostrarseEnMenu() => true;

    public string ObtenerEtiqueta()
    {
        switch (accion)
        {
            case Accion.Modo:
                return "Metro audio: modo " + (metronomo != null && metronomo.ModoAudio == ModoAudioMetronomo.UnoPorTic ? "1 por tic" : "programado");
            case Accion.ClickPrueba:
                return "Metro: click de prueba";
            default:
                return "Metro: DSP buffer " + AudioSettings.GetConfiguration().dspBufferSize;
        }
    }

    void Awake()
    {
        if (panelPausa != null && itemsQueAmplia > 0)
            panelPausa.sizeDelta += new Vector2(0f, itemsQueAmplia * alturaExtraPorItem);
    }

    /// <summary>Acción del ítem del menú de pausa.</summary>
    public void Ejecutar()
    {
        if (metronomo == null) { Debug.Log("[METRO] ítem de depuración sin MetronomoRCP", this); return; }
        switch (accion)
        {
            case Accion.Modo: metronomo.AlternarModoAudio(); break;
            case Accion.ClickPrueba: metronomo.ClickDePrueba(); break;
            case Accion.DspBuffer: CambiarDspBuffer(); break;
        }
    }

    private void CambiarDspBuffer()
    {
        var cfg = AudioSettings.GetConfiguration();
        int antes = cfg.dspBufferSize;
        int nuevo = antes <= 256 ? 512 : (antes <= 512 ? 1024 : 256);
        cfg.dspBufferSize = nuevo;
        bool zumbidoAntes = ambiente != null && ambiente.Encendido && AmbienteSuena();
        bool ok = AudioSettings.Reset(cfg);
        bool zumbidoTrasReset = AmbienteSuena();
        Debug.Log("[METRO] DSP buffer " + antes + " -> " + nuevo + " con AudioSettings.Reset: ok=" + ok
                  + " | el zumbido sonaba antes=" + zumbidoAntes + ", sigue sonando justo tras Reset=" + zumbidoTrasReset
                  + (zumbidoAntes && !zumbidoTrasReset ? " => Reset CORTÓ el audio (el zumbido y el metrónomo se recuperan solos 0,1 s después)" : ""), this);
    }

    private bool AmbienteSuena()
    {
        if (ambiente == null) return false;
        var f = ambiente.GetComponent<AudioSource>();
        return f != null && f.isPlaying;
    }
}
