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
    public float escalaBaseMetronomo = 1f;
    public float pulsoMetronomo = 0.25f;
    public float escalaBaseCirculoRitmo = 208f;

    [Header("Efecto de acierto (estilo Guitar Hero)")]
    public Transform aroHit;
    public Renderer rendererAroHit;
    public float duracionHit = 0.3f;
    public float escalaPunch = 1.3f;
    public float escalaAroMax = 2.2f;
    public Color colorFlashHit = Color.white;

    private float tiempoUltimoHit = -10f;
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
        if (circuloMetronomo == null || bpmObjetivo <= 0f) return;

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

        if (textoRitmo != null)
            textoRitmo.text = bpm > 0f ? $"{bpm:F0} BPM" : "-- BPM";

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
            aroHit.gameObject.SetActive(enHit);
            if (enHit)
            {
                float escalaAro = Mathf.Lerp(escalaBaseCirculoRitmo, escalaBaseCirculoRitmo * escalaAroMax, t);
                Vector3 sAro = aroHit.localScale;
                aroHit.localScale = new Vector3(escalaAro, sAro.y, escalaAro);
                if (rendererAroHit != null)
                {
                    Color colorAro = colorFlashHit;
                    colorAro.a = 1f - t;
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
            textoValorProfundidad.text = logica.EstaEmpujando ? $"{mm:F0}mm" : "--";
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
