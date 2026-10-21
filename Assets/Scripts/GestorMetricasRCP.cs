using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GestorMetricasRCP : MonoBehaviour
{
    public LogicaRCP logica;

    [Header("Ritmo (BPM)")]
    public TextMeshProUGUI textoRitmo;
    public Transform circuloMetronomo;
    public Transform circuloRitmoTransform;
    public Renderer rendererCirculoRitmo;
    public float bpmObjetivo = 110f;
    public float bpmMinimo = 100f;
    public float bpmMaximo = 120f;
    public float escalaBaseMetronomo = 288f; // = escala local de Circulo_Metronomo en la escena (anillo exterior al Circulo_Ritmo de 208)
    public float pulsoMetronomo = 0.25f;
    public float escalaBaseCirculoRitmo = 208f;

    [Header("Efecto de acierto (estilo Guitar Hero)")]
    public Transform aroHit;
    public Renderer rendererAroHit;
    public float duracionHit = 0.3f;
    public float escalaPunch = 1.3f;
    public float escalaAroMax = 1.9f; // 208×1.9 = 395,2 u de diámetro, centro (-190,-40): x[-387,8] y[-238,158] → dentro del panel (±400×±240) y por debajo del encabezado (y>=160); con 2,0 rebasaba 8 u en y
    public Color colorFlashHit = Color.white;

    [Header("Metrónomo")]
    [Tooltip("Reloj único (dspTime) compartido con el sonido: el pico del pulso coincide con el tic. En pausa y " +
             "con la sesión Terminada el pulso se congela en reposo; al reanudar se re-ancla la fase. Si es null " +
             "se usa el pulso antiguo (Time.time % intervalo a bpmObjetivo, sin sincronía con el audio).")]
    public MetronomoRCP metronomo;

    [Header("Sesión de RCP")]
    [Tooltip("Solo para el pulso antiguo (cuando 'metronomo' es null). Opcional. Si se asigna, el pulso visual del metrónomo se detiene cuando la sesión " +
             "llega a Terminada y se reanuda al empezar una nueva (Repetir sesión), igual que el " +
             "metrónomo sonoro de LogicaRCP. Solo lee Estado: sin asignaciones por fotograma.")]
    public SesionRCP sesion;
    private bool _metronomoDetenido = false;

    private float tiempoUltimoHit = -10f;
    private const int CLAVE_MM_SIN_EMPUJAR = int.MinValue + 1;
    private int _mmMostrado = int.MinValue;  // caché del entero mostrado en textoValorProfundidad (int.MinValue = nada escrito aún)
    private int _bpmMostrado = int.MinValue; // caché del entero mostrado en textoRitmo (int.MinValue = nada escrito aún)
    private bool estabaEmpujandoAnterior = false;

    [Header("Profundidad")]
    public RectTransform marcadorProfundidad;
    public Image imagenMarcadorProfundidad;
    public TextMeshProUGUI textoValorProfundidad;
    public float mmMinimoBarra = 0f;
    public float mmMaximoBarra = 70f;
    public float yInferiorBarra = -180f;
    public float ySuperiorBarra = 100f;
    public float profundidadMinimaCorrecta = 50f;
    public float profundidadMaximaCorrecta = 60f;
    public float profundidadPeligrosa = 70f;

    [Header("Colores")]
    public Color colorCorrecto = new Color(0.15f, 0.85f, 0.35f);
    public Color colorAdvertencia = new Color(0.95f, 0.85f, 0.15f);
    public Color colorPeligro = new Color(0.9f, 0.2f, 0.2f);
    public Color colorNeutro = new Color(0.4f, 0.75f, 0.85f);

    void Update()
    {
        if (logica == null) return;

        ActualizarMetronomo();
        ActualizarRitmo();
        ActualizarProfundidad();
    }

    void ActualizarMetronomo()
    {
        if (circuloMetronomo == null) return;

        if (metronomo != null)
        {
            float faseDsp = metronomo.Fase01();       // −1 = detenido (pausa / sesión Terminada)
            if (faseDsp < 0f)
            {
                if (!_metronomoDetenido)
                {
                    _metronomoDetenido = true;
                    Vector3 sRep = circuloMetronomo.localScale;
                    circuloMetronomo.localScale = new Vector3(escalaBaseMetronomo, sRep.y, escalaBaseMetronomo);
                }
                return;
            }
            _metronomoDetenido = false;
            float escalaDsp = escalaBaseMetronomo * (1f + (1f - faseDsp) * pulsoMetronomo);
            Vector3 sDsp = circuloMetronomo.localScale;
            circuloMetronomo.localScale = new Vector3(escalaDsp, sDsp.y, escalaDsp);
            return;
        }

        if (bpmObjetivo <= 0f) return;

        // Fin de sesión: el pulso visual se congela en reposo (escala base) hasta que la sesión
        // deje de estar Terminada. En pausa y Calibración NO cambia (sigue pulsando como antes).
        if (sesion != null && sesion.Estado == EstadoSesionRCP.Terminada)
        {
            if (!_metronomoDetenido)
            {
                _metronomoDetenido = true;
                Vector3 sBase = circuloMetronomo.localScale;
                circuloMetronomo.localScale = new Vector3(escalaBaseMetronomo, sBase.y, escalaBaseMetronomo);
            }
            return;
        }
        _metronomoDetenido = false;

        float intervalo = 60f / bpmObjetivo;
        float fase = (Time.time % intervalo) / intervalo;
        float pulso = 1f - fase;
        float escala = escalaBaseMetronomo * (1f + pulso * pulsoMetronomo);
        Vector3 s = circuloMetronomo.localScale;
        circuloMetronomo.localScale = new Vector3(escala, s.y, escala);
    }

    void ActualizarRitmo()
    {
        float bpm = logica.BpmActual;
        bool enRitmo = bpm >= bpmMinimo && bpm <= bpmMaximo;
        bool empujandoAhora = logica.EstaEmpujando;

        if (empujandoAhora && !estabaEmpujandoAnterior && enRitmo)
            tiempoUltimoHit = Time.time;
        estabaEmpujandoAnterior = empujandoAhora;

        // El texto se reconstruye solo cuando cambia el entero mostrado (antes: cadena nueva en cada fotograma).
        // Mismo redondeo que {bpm:F0} (mitades hacia arriba); bpm <= 0 muestra "-- BPM".
        if (textoRitmo != null)
        {
            int bpmMostrar = bpm > 0f ? (int)(bpm + 0.5f) : -1;
            if (bpmMostrar != _bpmMostrado)
            {
                _bpmMostrado = bpmMostrar;
                textoRitmo.text = bpmMostrar < 0 ? "-- BPM" : bpmMostrar.ToString() + " BPM";
            }
        }

        float transcurrido = Time.time - tiempoUltimoHit;
        bool enHit = transcurrido < duracionHit;
        float t = enHit ? transcurrido / duracionHit : 1f;
        float pulso = enHit ? (1f - t * t) : 0f;

        Color colorBase = enRitmo ? colorCorrecto : colorNeutro;
        Color colorFinal = Color.Lerp(colorBase, colorFlashHit, pulso);
        float escala = 1f + pulso * (escalaPunch - 1f);

        if (rendererCirculoRitmo != null)
            rendererCirculoRitmo.material.color = colorFinal;

        if (circuloRitmoTransform != null)
        {
            Vector3 s = circuloRitmoTransform.localScale;
            float lado = escalaBaseCirculoRitmo * escala;
            circuloRitmoTransform.localScale = new Vector3(lado, s.y, lado);
        }

        if (aroHit != null)
        {
            // Aro_Hit es la señal "presiona ahora": un destello por cada click del metrónomo, derivado de la misma
            // rejilla dsp (MetronomoRCP.TryDestelloTic). Ya no reacciona al descenso del usuario ni a BpmActual.
            // Sin metrónomo cableado se conserva el disparo antiguo (flanco de EstaEmpujando).
            float tAro = t;
            bool aroVisible = enHit;
            if (metronomo != null)
            {
                aroVisible = metronomo.TryDestelloTic(out float seg) && seg < duracionHit;
                tAro = aroVisible ? seg / duracionHit : 1f;
            }
            aroHit.gameObject.SetActive(aroVisible);
            if (aroVisible)
            {
                float escalaAro = Mathf.Lerp(escalaBaseCirculoRitmo, escalaBaseCirculoRitmo * escalaAroMax, tAro);
                Vector3 sAro = aroHit.localScale;
                aroHit.localScale = new Vector3(escalaAro, sAro.y, escalaAro);
                if (rendererAroHit != null)
                {
                    Color colorAro = colorFlashHit;
                    colorAro.a = 1f - tAro;
                    rendererAroHit.material.color = colorAro;
                }
            }
        }
    }

    void ActualizarProfundidad()
    {
        float mm = logica.EstaEmpujando ? logica.ProfundidadActualCM * 10f : 0f;
        float mmClamp = Mathf.Clamp(mm, mmMinimoBarra, mmMaximoBarra);
        float t = (mmClamp - mmMinimoBarra) / (mmMaximoBarra - mmMinimoBarra);
        float y = Mathf.Lerp(yInferiorBarra, ySuperiorBarra, t);

        if (marcadorProfundidad != null)
        {
            Vector2 pos = marcadorProfundidad.anchoredPosition;
            pos.y = y;
            marcadorProfundidad.anchoredPosition = pos;
        }

        Color colorActual = ColorParaProfundidad(mm);

        if (imagenMarcadorProfundidad != null)
            imagenMarcadorProfundidad.color = colorActual;

        if (textoValorProfundidad != null)
        {
            // El texto se reconstruye solo cuando cambia el entero de mm mostrado (antes: cadena nueva en cada
            // fotograma mientras se empuja). Mismo redondeo que {mm:F0} (mitades alejándose de cero); sin empujar
            // muestra "--" (clave propia), igual que antes. color y posición se siguen actualizando por fotograma.
            int claveMm;
            if (!logica.EstaEmpujando) claveMm = CLAVE_MM_SIN_EMPUJAR;
            else if (mm >= 0f) claveMm = (int)(mm + 0.5f);
            else claveMm = (int)(mm - 0.5f);
            if (claveMm != _mmMostrado)
            {
                _mmMostrado = claveMm;
                textoValorProfundidad.text = claveMm == CLAVE_MM_SIN_EMPUJAR ? "--" : claveMm.ToString() + "mm";
            }
            textoValorProfundidad.color = colorActual;
            Vector2 posTexto = textoValorProfundidad.rectTransform.anchoredPosition;
            posTexto.y = y;
            textoValorProfundidad.rectTransform.anchoredPosition = posTexto;
        }
    }

    Color ColorParaProfundidad(float mm)
    {
        if (mm <= 0f) return Color.white;
        if (mm > profundidadPeligrosa) return colorPeligro;
        if (mm >= profundidadMinimaCorrecta && mm <= profundidadMaximaCorrecta) return colorCorrecto;
        return colorAdvertencia;
    }
}
