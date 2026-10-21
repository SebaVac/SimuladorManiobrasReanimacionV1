using System.Text;
using UnityEngine;

/// <summary>Cómo sale el click del metrónomo. En ambos modos la rejilla dsp (<c>ancla + k·intervalo</c>) y el destello
/// visual son los mismos; solo cambia cómo se emite el audio.</summary>
public enum ModoAudioMetronomo
{
    /// <summary>Un <c>PlayOneShot</c> sobre el AudioSource original (el mismo camino que sonaba antes de MetronomoRCP) en el
    /// fotograma en que el tic de la rejilla se cumple. Retraso respecto a la rejilla: como máximo un fotograma.</summary>
    UnoPorTic,
    /// <summary>Cada tic se programa con <c>PlayScheduled</c> sobre 2 fuentes alternadas, con anticipo (precisión de muestra).</summary>
    Programado,
}

/// <summary>
/// Metrónomo único de SceneSimulador: UNA referencia temporal para el sonido y el pulso visual,
/// basada en el reloj de audio (<see cref="AudioSettings.dspTime"/>).
///
/// Antes: el audio salía por <c>Time.time &gt;= proximoTic</c> + <c>PlayOneShot</c> (cuantizado al fotograma,
/// con deriva: cada tic se reprogramaba desde el fotograma en que salió, ~109,5 cpm reales) y el pulso visual
/// era <c>Time.time % intervalo</c> a 110 fijo desde t=0 (otra fase y otro tempo). Ahora ambos salen de la misma
/// rejilla <c>ancla + k·intervalo</c>:
///  - Audio: cada tic se programa con <c>PlayScheduled</c> exactamente en la rejilla (precisión de muestra).
///  - Visual: <see cref="Fase01"/> da la fase de esa misma rejilla; el pico del pulso (fase 0) cae en el tic.
///
/// La fase se ancla al ENTRAR EN LA ZONA de RCP (el mismo punto donde antes arrancaba el audio), al
/// reiniciar/cambiar de modo y al reanudar tras la pausa. En pausa y con la sesión Terminada ambos se detienen
/// (<see cref="Detener"/>). En Calibración no suena (nadie programa tics) y el visual sigue corriendo sobre la
/// rejilla, como antes. bpm: <see cref="LogicaRCP.bpmMetronomo"/> (fuente única; GestorMetricasRCP.bpmObjetivo
/// ya no se usa).
///
/// dspTime se actualiza por bloques de buffer DSP (1024 muestras ≈ 21 ms), así que se interpola con el reloj de
/// tiempo real para que el visual avance suave. Cero asignaciones por fotograma.
/// </summary>
public class MetronomoRCP : MonoBehaviour
{
    [SerializeField] private LogicaRCP logica;

    [Header("Programación del audio")]
    [Tooltip("Cuántos segundos por delante se programa cada tic con PlayScheduled.")]
    [SerializeField] private float anticipoProgramacionSeg = 0.2f;
    [Tooltip("Margen entre el anclaje y el primer tic (nunca programar en el pasado).")]
    [SerializeField] private float margenInicialSeg = 0.1f;

    [Header("Modo de audio")]
    [Tooltip("UnoPorTic: PlayOneShot en el fotograma en que el tic de la rejilla se cumple (camino que sonaba antes). " +
             "Programado: PlayScheduled con anticipo sobre 2 fuentes alternadas.")]
    [SerializeField] private ModoAudioMetronomo modoAudio = ModoAudioMetronomo.UnoPorTic;
    [Tooltip("UnoPorTic: un tic que lleva más de este tiempo vencido (hipo largo) se descarta, sin ráfaga.")]
    [SerializeField] private float toleranciaTicVencidoSeg = 0.05f;

    [Header("Registro [METRO] (logcat)")]
    [Tooltip("Cuántos tics se registran tras cada anclaje (además de cada cambio de estado). 0 = sin registro de tics.")]
    [SerializeField] private int ticsARegistrar = 5;
    [Tooltip("Segundos tras el tic en que se vuelve a leer isPlaying/timeSamples de las fuentes.")]
    [SerializeField] private float chequeoPosteriorSeg = 0.03f;

    [Header("Latencia de salida de audio")]
    [Tooltip("Valor positivo retrasa el destello respecto al click. Se aplica al destello de Aro_Hit y al pico del " +
             "pulso de Circulo_Metronomo, para compensar la latencia de salida de audio del dispositivo (el sonido " +
             "sale unos ms después de su hora dsp). 0 = sin compensación; ajustar en el visor.")]
    [SerializeField] private float compensacionLatenciaVisualSeg = 0f;

    /// <summary>Solo para pruebas: si no es null reemplaza al reloj de audio (segundos).</summary>
    public System.Func<double> relojPrueba;

    private AudioSource[] _fuentes;
    private AudioSource _modelo;           // AudioSource original (altavoz): camino de PlayOneShot, el que sonaba antes
    private AudioClip _bip;
    private bool _listo;
    private bool _anclado;
    private bool _detenido;
    private double _ancla;
    private long _siguiente;
    private long _ultimoProgramado = -1;   // índice del último tic realmente programado (el que suena y destella)

    // Registro [METRO]
    private readonly StringBuilder _sb = new StringBuilder(640);
    private AudioListener _listener;
    private int _listenersActivos;
    private int _ticsRegistrados;
    private long _chequeoK = -1;
    private double _chequeoAt;
    private int _frameProgramar = -10;     // último fotograma en que LogicaRCP llamó a Programar (zona de RCP activa)
    private bool _suscrito;

    // Reloj dsp interpolado
    private double _offset;
    private bool _offsetInicial;
    private double _realPrevio;

    /// <summary>true = pausa o sesión Terminada: ni suena ni pulsa.</summary>
    public bool Detenido => _detenido;
    /// <summary>Cantidad de tics programados desde el último anclaje (solo verificación).</summary>
    public long TicsProgramados { get; private set; }
    /// <summary>Hora dsp del último tic programado (solo verificación).</summary>
    public double UltimoTicProgramado { get; private set; }
    public double Ancla => _ancla;
    public ModoAudioMetronomo ModoAudio => modoAudio;
    /// <summary>Mayor retraso (s) entre el instante de la rejilla y el fotograma en que se emitió el click (UnoPorTic);
    /// solo verificación.</summary>
    public double MaxRetrasoEmisionSeg { get; private set; }
    public double UltimoRetrasoEmisionSeg { get; private set; }
    /// <summary>Índice del último tic de la rejilla emitido/programado (verificación).</summary>
    public long UltimoIndiceProgramado => _ultimoProgramado;
    public double IntervaloSeg => logica != null && logica.bpmMetronomo > 0f ? 60.0 / logica.bpmMetronomo : 60.0 / 110.0;

    /// <summary>LogicaRCP entrega el clip procedural y el AudioSource de referencia al arrancar.</summary>
    public void Inicializar(AudioClip bip, AudioSource modelo)
    {
        _bip = bip;
        _modelo = modelo;
        _fuentes = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            // Una fuente por tic en vuelo: PlayScheduled sobre una fuente ya programada la reiniciaría.
            var f = modelo != null ? modelo.gameObject.AddComponent<AudioSource>() : gameObject.AddComponent<AudioSource>();
            if (modelo != null)
            {
                f.outputAudioMixerGroup = modelo.outputAudioMixerGroup;
                f.volume = modelo.volume; f.pitch = modelo.pitch; f.priority = modelo.priority;
                f.spatialBlend = modelo.spatialBlend; f.spatialize = modelo.spatialize;
                f.minDistance = modelo.minDistance; f.maxDistance = modelo.maxDistance; f.rolloffMode = modelo.rolloffMode;
            }
            f.playOnAwake = false; f.loop = false; f.clip = bip;
            _fuentes[i] = f;
        }
        _listo = true;
        if (!_suscrito) { AudioSettings.OnAudioConfigurationChanged += AlCambiarConfiguracionAudio; _suscrito = true; }
        Anclar();
        RegistrarEstado("inicializado");
    }

    void OnDestroy()
    {
        if (_suscrito) AudioSettings.OnAudioConfigurationChanged -= AlCambiarConfiguracionAudio;
        _suscrito = false;
    }

    /// <summary>Unity detiene TODAS las fuentes cuando cambia la configuración de audio (AudioSettings.Reset o cambio de
    /// dispositivo/ruta). El reloj interpolado se recalcula y la rejilla se re-ancla para no quedar con tics programados
    /// que ya no existen.</summary>
    private void AlCambiarConfiguracionAudio(bool dispositivoCambio)
    {
        _offsetInicial = false;
        RegistrarEstado("configuracion de audio cambiada (dispositivo=" + dispositivoCambio + ")");
        CancelInvoke(nameof(RecuperarTrasCambioDeAudio));
        Invoke(nameof(RecuperarTrasCambioDeAudio), 0.1f);   // el motor aún se está reiniciando dentro del evento
    }

    /// <summary>AudioSettings.Reset invalida los clips procedurales (AudioClip.Create: quedan con longitud 0): se recrea el
    /// click si hace falta, se reasigna a las fuentes y se re-ancla la rejilla.</summary>
    private void RecuperarTrasCambioDeAudio()
    {
        if (!_listo) return;
        bool invalido = _bip == null || _bip.length <= 0f;
        if (invalido)
        {
            _bip = CrearBipInterno();
            for (int i = 0; i < _fuentes.Length; i++) _fuentes[i].clip = _bip;
        }
        if (_anclado && !_detenido) Anclar();
        RegistrarEstado("recuperado tras el cambio de audio (clip recreado=" + invalido + ")");
    }

    // Misma síntesis que LogicaRCP.CrearSonidoBip (1 kHz, 0,1 s, 44100 Hz, mono), solo para recrearlo tras un cambio de audio.
    private static AudioClip CrearBipInterno()
    {
        const int sampleRate = 44100;
        int length = (int)(sampleRate * 0.1f);
        float[] samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            samples[i] = Mathf.Sin(2 * Mathf.PI * 1000f * i / sampleRate);
            if (i > length - 1000) samples[i] *= (length - i) / 1000f;
        }
        AudioClip clip = AudioClip.Create("BipProcedural", length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>Reloj dsp (s). dspTime avanza a saltos de un buffer; se interpola con el tiempo real tomando
    /// el máximo de (dsp − real) —la lectura justo tras cada salto es la exacta— con un decaimiento mínimo
    /// para seguir la deriva entre ambos relojes.</summary>
    public double Ahora()
    {
        if (relojPrueba != null) return relojPrueba();
        double real = Time.realtimeSinceStartupAsDouble;
        double s = AudioSettings.dspTime - real;
        if (!_offsetInicial || s > _offset) { _offset = s; _offsetInicial = true; }
        else _offset -= (real - _realPrevio) * 1e-4;
        _realPrevio = real;
        return real + _offset;
    }

    /// <summary>Re-ancla la fase: el próximo pico (y tic) cae <c>margenInicialSeg</c> adelante. Reanuda
    /// si estaba detenido.</summary>
    public void Anclar()
    {
        if (!_listo) return;
        CancelarPendientes();
        _ancla = Ahora() + margenInicialSeg;
        _siguiente = 0;
        _ultimoProgramado = -1;
        TicsProgramados = 0;
        _detenido = false;
        _anclado = true;
        _ticsRegistrados = 0;
        _chequeoK = -1;
        MaxRetrasoEmisionSeg = 0.0; UltimoRetrasoEmisionSeg = 0.0;
        RegistrarEstado("anclado");
    }

    /// <summary>Pausa o fin de sesión: cancela los tics ya programados y congela el pulso visual.</summary>
    public void Detener()
    {
        _detenido = true;
        CancelarPendientes();
        RegistrarEstado("detenido");
    }

    /// <summary>Alterna UnoPorTic / Programado (ítem de depuración). Cancela lo programado y re-ancla la rejilla.</summary>
    public void AlternarModoAudio()
    {
        modoAudio = modoAudio == ModoAudioMetronomo.UnoPorTic ? ModoAudioMetronomo.Programado : ModoAudioMetronomo.UnoPorTic;
        CancelarPendientes();
        if (_listo && _anclado && !_detenido) Anclar();
        RegistrarEstado("modo de audio -> " + modoAudio);
    }

    /// <summary>Click suelto (ítem de depuración): PlayOneShot directo sobre el AudioSource original, sin rejilla.</summary>
    public void ClickDePrueba()
    {
        AudioSource f = _modelo != null ? _modelo : (_fuentes != null ? _fuentes[0] : null);
        if (f == null || _bip == null) { Debug.Log("[METRO] click de prueba: sin fuente o sin clip"); return; }
        f.PlayOneShot(_bip);
        _sb.Length = 0;
        _sb.Append("[METRO] click de prueba (PlayOneShot suelto en '").Append(f.name).Append("') dsp=").Append(AudioSettings.dspTime.ToString("0.000"))
           .Append(" | fuente: ");
        AgregarFuente(_sb, f); AgregarListener(_sb);
        Debug.Log(_sb.ToString(), this);
    }

    /// <summary>Lo llama LogicaRCP en cada fotograma de RCP en zona: programa los tics que caen dentro del
    /// anticipo sobre la rejilla ancla + k·intervalo.</summary>
    public void Programar()
    {
        if (!_listo || !_anclado || _detenido || _bip == null) return;
        double ahora = Ahora();
        double iv = IntervaloSeg;
        _frameProgramar = Time.frameCount;
        if (_chequeoK >= 0 && ahora >= _chequeoAt) Chequeo();

        if (modoAudio == ModoAudioMetronomo.UnoPorTic)
        {
            // El tic k se emite en el primer fotograma en que su hora de rejilla ya llegó. La rejilla, TicsProgramados
            // y el destello visual son los mismos que en el modo programado; solo cambia cómo se emite el audio.
            while (_ancla + _siguiente * iv <= ahora)
            {
                double t = _ancla + _siguiente * iv;
                double retraso = ahora - t;
                if (retraso <= toleranciaTicVencidoSeg)       // tics muy vencidos (hipo largo) se descartan, sin ráfaga
                {
                    AudioSource f = _modelo != null ? _modelo : _fuentes[0];
                    f.PlayOneShot(_bip);
                    _ultimoProgramado = _siguiente;
                    TicsProgramados++;
                    UltimoTicProgramado = t;
                    UltimoRetrasoEmisionSeg = retraso;
                    if (retraso > MaxRetrasoEmisionSeg) MaxRetrasoEmisionSeg = retraso;
                    RegistrarTic(_siguiente, t, ahora, f);
                }
                _siguiente++;
            }
            return;
        }

        while (_ancla + _siguiente * iv <= ahora + anticipoProgramacionSeg)
        {
            double t = _ancla + _siguiente * iv;
            if (t >= ahora - 0.02)               // tics ya vencidos (hipo largo) se descartan, sin ráfaga
            {
                AudioSource f = _fuentes[(int)(_siguiente & 1)];
                f.PlayScheduled(t);
                _ultimoProgramado = _siguiente;
                TicsProgramados++;
                UltimoTicProgramado = t;
                RegistrarTic(_siguiente, t, ahora, f);
            }
            _siguiente++;
        }
    }

    /// <summary>Fase del pulso visual en [0,1): 0 = pico (instante del tic), crece hasta el siguiente.
    /// −1 si está detenido.</summary>
    public float Fase01()
    {
        if (!_listo || !_anclado || _detenido) return -1f;
        double x = (Ahora() - _ancla - compensacionLatenciaVisualSeg) / IntervaloSeg;
        return (float)(x - System.Math.Floor(x));
    }

    /// <summary>Destello por tic (Aro_Hit): true mientras el tic MÁS RECIENTE de la rejilla sea uno de los que
    /// realmente se programaron (es decir, mientras el metrónomo suena), con los segundos transcurridos desde
    /// ese tic (más <see cref="compensacionLatenciaVisualSeg"/>). Sin estado ni asignaciones: la animación se deriva
    /// del reloj, así que parte del instante exacto del click y no del fotograma. false en pausa, con la sesión
    /// Terminada, antes del primer tic, en Calibración y fuera de la zona de RCP (nadie programa tics ahí).</summary>
    public bool TryDestelloTic(out float segundosDesdeTic)
    {
        segundosDesdeTic = 0f;
        if (!_listo || !_anclado || _detenido || _ultimoProgramado < 0) return false;
        double iv = IntervaloSeg;
        double x = (Ahora() - _ancla - compensacionLatenciaVisualSeg) / iv;
        if (x < 0.0) return false;                         // todavía no llega el primer tic
        long k = (long)System.Math.Floor(x);
        if (k > _ultimoProgramado)
        {
            // UnoPorTic: el click sale desde Programar() en el fotograma del tic. Si en este fotograma Programar() aún no
            // corrió (orden de Update), el tic recién cumplido se acepta mientras la zona de RCP siga activa (Programar
            // llamado en este fotograma o en el anterior), para que el destello y el click salgan juntos.
            bool recienCumplido = modoAudio == ModoAudioMetronomo.UnoPorTic && k == _ultimoProgramado + 1
                                  && Time.frameCount - _frameProgramar <= 1;
            if (!recienCumplido) return false;             // ese tic no se emitió: no suena, no destella
        }
        segundosDesdeTic = (float)((x - k) * iv);
        return true;
    }

    // ── Registro [METRO] ─────────────────────────────────────────────────────────────────────────────

    private void RegistrarTic(long k, double t, double ahora, AudioSource f)
    {
        if (_ticsRegistrados >= ticsARegistrar) return;
        _ticsRegistrados++;
        double dsp = AudioSettings.dspTime;
        _sb.Length = 0;
        _sb.Append("[METRO] tic #").Append(k).Append(" modo=").Append(modoAudio).Append(" clip=").Append(_bip.length.ToString("0.000")).Append(" s")
           .Append(" | dsp programado=").Append(t.ToString("0.0000")).Append(" dsp actual=").Append(dsp.ToString("0.0000"))
           .Append(" margen=").Append(((t - dsp) * 1000.0).ToString("0.0")).Append(" ms (negativo = ya vencido)")
           .Append(" | reloj interpolado - dsp=").Append(((ahora - dsp) * 1000.0).ToString("0.0")).Append(" ms")
           .Append(" | isPlaying tras emitir: f0=").Append(_fuentes != null && _fuentes[0].isPlaying).Append(" f1=").Append(_fuentes != null && _fuentes[1].isPlaying)
           .Append(" modelo=").Append(_modelo != null && _modelo.isPlaying).Append(" | fuente usada: ");
        AgregarFuente(_sb, f); AgregarListener(_sb);
        Debug.Log(_sb.ToString(), this);
        _chequeoK = k; _chequeoAt = ahora + (t > ahora ? t - ahora : 0.0) + chequeoPosteriorSeg;
    }

    private void Chequeo()
    {
        long k = _chequeoK; _chequeoK = -1;
        _sb.Length = 0;
        _sb.Append("[METRO] chequeo tic #").Append(k).Append(" (+").Append((chequeoPosteriorSeg * 1000f).ToString("0")).Append(" ms tras la hora del tic): isPlaying f0=")
           .Append(_fuentes[0].isPlaying).Append(" f1=").Append(_fuentes[1].isPlaying).Append(" modelo=").Append(_modelo != null && _modelo.isPlaying)
           .Append(" | timeSamples f0=").Append(_fuentes[0].timeSamples).Append(" f1=").Append(_fuentes[1].timeSamples)
           .Append(" (>0 = la voz ya se está renderizando)");
        Debug.Log(_sb.ToString(), this);
    }

    /// <summary>Registro de estado (arranque, anclaje, pausa, cambio de modo, cambio de configuración de audio).</summary>
    public void RegistrarEstado(string motivo)
    {
        if (!_listo) return;
        int bl, nb; AudioSettings.GetDSPBufferSize(out bl, out nb);
        _sb.Length = 0;
        _sb.Append("[METRO] estado: ").Append(motivo).Append(" | modo=").Append(modoAudio).Append(" bpm=").Append(logica != null ? logica.bpmMetronomo : 0f)
           .Append(" anclado=").Append(_anclado).Append(" detenido=").Append(_detenido)
           .Append(" | dsp actual=").Append(AudioSettings.dspTime.ToString("0.000"))
           .Append(" | DSP buffer ").Append(bl).Append('x').Append(nb).Append(" @").Append(AudioSettings.outputSampleRate).Append(" Hz")
           .Append(" | clip ").Append(_bip != null ? _bip.length.ToString("0.000") + " s " + _bip.frequency + " Hz " + _bip.channels + " canal(es) " + _bip.loadState : "null")
           .Append(" | modelo: ");
        AgregarFuente(_sb, _modelo);
        for (int i = 0; _fuentes != null && i < _fuentes.Length; i++) { _sb.Append(" | f").Append(i).Append(": "); AgregarFuente(_sb, _fuentes[i]); }
        AgregarListener(_sb);
        Debug.Log(_sb.ToString(), this);
    }

    private static void AgregarFuente(StringBuilder sb, AudioSource f)
    {
        if (f == null) { sb.Append("null"); return; }
        float efectivo = f.mute ? 0f : f.volume * (f.ignoreListenerVolume ? 1f : AudioListener.volume);
        sb.Append(f.name).Append(" enabled=").Append(f.enabled).Append(" vol=").Append(f.volume.ToString("0.00")).Append(" vol.efectivo=").Append(efectivo.ToString("0.00"))
          .Append(" mute=").Append(f.mute).Append(" spatialBlend=").Append(f.spatialBlend.ToString("0.0")).Append(" prio=").Append(f.priority)
          .Append(" isPlaying=").Append(f.isPlaying);
    }

    private void AgregarListener(StringBuilder sb)
    {
        if (_listener == null || !_listener.isActiveAndEnabled)
        {
            var ls = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            _listener = null;
            for (int i = 0; i < ls.Length; i++) if (ls[i].isActiveAndEnabled) { _listener = ls[i]; break; }
            _listenersActivos = ls.Length;
        }
        sb.Append(" | AudioListener efectivo=").Append(_listener != null ? _listener.name + " en " + _listener.transform.position.ToString("F2") : "NINGUNO")
          .Append(" (listeners en escena=").Append(_listenersActivos).Append(") AudioListener.volume=").Append(AudioListener.volume.ToString("0.00"))
          .Append(" pause=").Append(AudioListener.pause);
    }

    private void CancelarPendientes()
    {
        if (_fuentes == null) return;
        for (int i = 0; i < _fuentes.Length; i++) if (_fuentes[i] != null) _fuentes[i].Stop();
    }
}
