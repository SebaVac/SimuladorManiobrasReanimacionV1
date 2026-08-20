using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Efecto visual "glow" de los botones del menú principal al acercar el dedo (poke)
/// o al pasar el puntero sobre ellos. No depende de Meta Interaction SDK directamente:
/// una vez que el Canvas tiene el "Poke Interaction" (Building Blocks) conectado al
/// EventSystem mediante PointableCanvasModule, los toques generan los eventos de
/// puntero estándar de Unity UI (IPointerEnterHandler, etc.) que este script usa.
///
/// Cómo usarlo:
/// 1. Agrega este componente al mismo GameObject que tiene el Image de fondo del botón
///    (el sprite "pill_button" en modo Sliced).
/// 2. (Opcional) Arrastra un Image hijo con el sprite "pill_glow" al campo "Glow Borde"
///    para el halo de luz alrededor del botón.
/// </summary>
[RequireComponent(typeof(Image))]
public class BotonMenuFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Colores del fondo del botón")]
    public Color colorNormal = new Color(0.047f, 0.180f, 0.204f, 0.88f);
    public Color colorHover = new Color(0.09f, 0.32f, 0.36f, 0.92f);
    public Color colorPresionado = new Color(0.02f, 0.10f, 0.12f, 0.95f);

    [Header("Glow (Image hijo opcional con el sprite pill_glow)")]
    public Graphic glowBorde;
    [Range(0f, 1f)] public float glowAlphaNormal = 0.35f;
    [Range(0f, 1f)] public float glowAlphaHover = 1f;

    [Header("Transición")]
    public float velocidad = 10f;

    private Image fondo;
    private Color colorObjetivo;
    private float glowObjetivo;

    void Awake()
    {
        fondo = GetComponent<Image>();
        colorObjetivo = colorNormal;
        glowObjetivo = glowAlphaNormal;

        if (fondo != null) fondo.color = colorNormal;
        AplicarGlowInmediato(glowAlphaNormal);
    }

    void Update()
    {
        if (fondo != null)
            fondo.color = Color.Lerp(fondo.color, colorObjetivo, Time.deltaTime * velocidad);

        if (glowBorde != null)
        {
            Color c = glowBorde.color;
            c.a = Mathf.Lerp(c.a, glowObjetivo, Time.deltaTime * velocidad);
            glowBorde.color = c;
        }
    }

    void AplicarGlowInmediato(float alpha)
    {
        if (glowBorde == null) return;
        Color c = glowBorde.color;
        c.a = alpha;
        glowBorde.color = c;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        colorObjetivo = colorHover;
        glowObjetivo = glowAlphaHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        colorObjetivo = colorNormal;
        glowObjetivo = glowAlphaNormal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        colorObjetivo = colorPresionado;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        colorObjetivo = colorHover;
    }

    public void ActivarFeedbackManual()
    {
        OnPointerDown(null);
    }

    public void DesactivarFeedbackManual()
    {
        OnPointerExit(null);
    }
}
