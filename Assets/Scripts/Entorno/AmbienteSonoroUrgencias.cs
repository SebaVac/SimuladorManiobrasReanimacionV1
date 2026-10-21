using UnityEngine;

/// <summary>
/// Sonido ambiente de ventilación para los entornos de urgencias (EntornoBoxConsulta y EntornoSalaRCP): un zumbido
/// suave y continuo, generado de forma procedural (sin audio externo), con su PROPIO AudioSource, separado de los del
/// metrónomo. No tiene pitidos de monitor ni voces.
///
/// El clip se sintetiza UNA vez al arrancar y se reproduce en bucle sin clic: todas las componentes son periódicas en
/// la duración del clip (tonos con un número entero de ciclos y ruido filtrado en círculo). No tiene Update, así que
/// no añade asignaciones por fotograma. El volumen y el interruptor son campos serializados.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AmbienteSonoroUrgencias : MonoBehaviour
{
    [Header("Control")]
    [Tooltip("Interruptor: apaga o enciende el zumbido ambiente.")]
    [SerializeField] private bool encendido = true;
    [Range(0f, 1f)]
    [Tooltip("Volumen del AudioSource. Con el RMS del clip en ~0,05 y volumen 0,45 queda ~29 dB por debajo del click del metrónomo (RMS ~0,65 durante el click; ~22 dB si se promedia con los silencios entre clicks).")]
    [SerializeField] private float volumen = 0.45f;

    [Header("Síntesis (se genera una sola vez al arrancar)")]
    [Tooltip("Duración del bucle, en segundos. Los tonos usan un número entero de ciclos en esta duración.")]
    [SerializeField] private float duracionSeg = 2f;
    [Tooltip("RMS del clip generado (escala de -1 a 1). El click del metrónomo tiene un RMS de ~0,7.")]
    [SerializeField] private float rmsClip = 0.05f;
    [Tooltip("Frecuencia de la componente grave de la ventilación, en Hz (se ajusta a un número entero de ciclos).")]
    [SerializeField] private float frecuenciaBaseHz = 100f;
    [Tooltip("Semilla del ruido, para que el sonido sea siempre el mismo.")]
    [SerializeField] private int semilla = 7;

    private AudioSource _fuente;
    private AudioClip _clip;

    /// <summary>true si el zumbido está sonando (o debe sonar).</summary>
    public bool Encendido => encendido;

    /// <summary>RMS del clip generado (para verificación); 0 si aún no existe.</summary>
    public float RmsClip { get; private set; }

    /// <summary>Clip generado (solo verificación).</summary>
    public AudioClip Clip => _clip;

    void Awake()
    {
        _fuente = GetComponent<AudioSource>();
        _fuente.playOnAwake = false;
        _fuente.loop = true;
        _fuente.spatialBlend = 0f;                  // 2D: no depende de la posición de la cabeza
        _fuente.dopplerLevel = 0f;
        _fuente.outputAudioMixerGroup = null;
        GenerarClip();
        _fuente.clip = _clip;
        Aplicar();
    }

    void OnEnable()
    {
        Aplicar();
        AudioSettings.OnAudioConfigurationChanged += AlCambiarConfiguracionAudio;
    }

    void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= AlCambiarConfiguracionAudio;

    // AudioSettings.Reset (o un cambio de dispositivo) detiene todas las fuentes e invalida los clips procedurales
    // (AudioClip.Create). Se recupera el zumbido un instante después, cuando el motor ya terminó de reiniciarse.
    private void AlCambiarConfiguracionAudio(bool dispositivoCambio)
    {
        CancelInvoke(nameof(RecuperarTrasCambioDeAudio));
        Invoke(nameof(RecuperarTrasCambioDeAudio), 0.1f);
    }

    private void RecuperarTrasCambioDeAudio()
    {
        if (_fuente == null) return;
        if (_clip == null || _clip.length <= 0f)
        {
            GenerarClip();
            _fuente.clip = _clip;
        }
        Aplicar();
    }

    void OnValidate()
    {
        if (_fuente != null) Aplicar();
    }

    /// <summary>Enciende o apaga el zumbido ambiente.</summary>
    public void Encender(bool valor)
    {
        encendido = valor;
        Aplicar();
    }

    private void Aplicar()
    {
        if (_fuente == null || _clip == null) return;
        _fuente.volume = volumen;
        if (encendido && !_fuente.isPlaying && isActiveAndEnabled) _fuente.Play();
        else if (!encendido && _fuente.isPlaying) _fuente.Stop();
    }

    private void GenerarClip()
    {
        const int fs = 44100;
        int n = Mathf.Max(fs, Mathf.RoundToInt(duracionSeg * fs));
        float dur = n / (float)fs;
        // Frecuencias con número entero de ciclos en 'dur' => empalme perfecto al repetir el bucle
        float f1 = Mathf.Max(1, Mathf.Round(frecuenciaBaseHz * dur)) / dur;
        float f2 = 2f * f1, f3 = 3f * f1;

        var muestras = new float[n];
        double dosPiDivFs = 2.0 * System.Math.PI / fs;
        for (int i = 0; i < n; i++)
        {
            double t = i * dosPiDivFs;
            double lfo = 1.0 + 0.06 * System.Math.Sin(t * fs / n);   // 1 ciclo exacto en el bucle: respiración leve
            muestras[i] = (float)(lfo * (0.55 * System.Math.Sin(t * f1) + 0.22 * System.Math.Sin(t * f2 + 0.7) + 0.10 * System.Math.Sin(t * f3 + 1.9)));
        }

        // Ruido de banda baja (paso bajo de un polo aplicado en círculo: dos vueltas, se conserva la segunda)
        var rng = new System.Random(semilla);
        var ruido = new float[n];
        for (int i = 0; i < n; i++) ruido[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        float a = Mathf.Exp(-2f * Mathf.PI * 900f / fs);           // corte ~900 Hz
        float y = 0f;
        for (int pasada = 0; pasada < 2; pasada++)
            for (int i = 0; i < n; i++)
            {
                y = a * y + (1f - a) * ruido[i];
                if (pasada == 1) ruido[i] = y;
            }
        double sumaR = 0; for (int i = 0; i < n; i++) sumaR += ruido[i] * ruido[i];
        float escalaRuido = (float)(0.35 / System.Math.Max(1e-6, System.Math.Sqrt(sumaR / n)));
        for (int i = 0; i < n; i++) muestras[i] += ruido[i] * escalaRuido * 0.25f;

        // Sin componente continua y normalizado al RMS pedido
        double media = 0; for (int i = 0; i < n; i++) media += muestras[i]; media /= n;
        double suma = 0; for (int i = 0; i < n; i++) { muestras[i] -= (float)media; suma += muestras[i] * muestras[i]; }
        float k = (float)(rmsClip / System.Math.Max(1e-6, System.Math.Sqrt(suma / n)));
        double sumaFinal = 0;
        for (int i = 0; i < n; i++) { muestras[i] *= k; sumaFinal += muestras[i] * muestras[i]; }
        RmsClip = (float)System.Math.Sqrt(sumaFinal / n);

        _clip = AudioClip.Create("AmbienteVentilacionProcedural", n, 1, fs, false);
        _clip.SetData(muestras, 0);
    }

    void OnDestroy()
    {
        if (_clip != null) Destroy(_clip);
    }
}
