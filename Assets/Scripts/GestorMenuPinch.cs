using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GestorMenuPinch : MonoBehaviour
{
    [Header("Referencias OVR")]
    public OVRSkeleton esqueletoIzquierdo;
    public OVRSkeleton esqueletoDerecho;

    [Header("Menú")]
    [Tooltip("Contenedor cuyos botones hijos (activos) se evalúan para la selección por toque.")]
    public Transform contenedorBotones;

    [Header("Detección de toque")]
    [Tooltip("Tolerancia de profundidad frente/detrás del plano del botón, en metros.")]
    public float profundidadToque = 0.05f;

    [Header("Feedback")]
    public float duracionFeedback = 0.15f;

    private Button botonTocadoIzq;
    private Button botonTocadoDer;
    private readonly Vector3[] _esquinas = new Vector3[4];

    void Update()
    {
        ProcesarMano(esqueletoIzquierdo, ref botonTocadoIzq);
        ProcesarMano(esqueletoDerecho, ref botonTocadoDer);
    }

    void ProcesarMano(OVRSkeleton esqueleto, ref Button botonTocadoAnterior)
    {
        if (esqueleto == null || contenedorBotones == null) return;
        if (!esqueleto.IsInitialized || !esqueleto.IsDataValid) return;

        Transform puntaDedo = ObtenerPuntaIndice(esqueleto);
        if (puntaDedo == null) return;

        Button botonActual = BuscarBotonTocado(puntaDedo.position);

        if (botonActual != null && botonActual != botonTocadoAnterior)
        {
            var feedback = botonActual.GetComponent<BotonMenuFeedback>();
            if (feedback != null) StartCoroutine(FlashFeedback(feedback));
            botonActual.onClick.Invoke();
        }

        botonTocadoAnterior = botonActual;
    }

    Transform ObtenerPuntaIndice(OVRSkeleton esqueleto)
    {
        var huesos = esqueleto.Bones;
        if (huesos == null) return null;

        for (int i = 0; i < huesos.Count; i++)
        {
            if (huesos[i].Id == OVRSkeleton.BoneId.Hand_IndexTip)
                return huesos[i].Transform;
        }
        return null;
    }

    Button BuscarBotonTocado(Vector3 puntaDedo)
    {
        Button[] botones = contenedorBotones.GetComponentsInChildren<Button>(false);

        foreach (Button b in botones)
        {
            if (EstaTocando(b.transform as RectTransform, puntaDedo))
                return b;
        }
        return null;
    }

    bool EstaTocando(RectTransform rt, Vector3 puntaDedo)
    {
        if (rt == null) return false;

        rt.GetWorldCorners(_esquinas);
        // _esquinas: 0=abajo-izq, 1=arriba-izq, 2=arriba-der, 3=abajo-der

        Vector3 origen = _esquinas[0];
        Vector3 ejeX = _esquinas[3] - _esquinas[0];
        Vector3 ejeY = _esquinas[1] - _esquinas[0];
        float largoX = ejeX.magnitude;
        float largoY = ejeY.magnitude;
        if (largoX <= 0f || largoY <= 0f) return false;
        ejeX /= largoX;
        ejeY /= largoY;
        Vector3 normal = Vector3.Cross(ejeX, ejeY).normalized;

        Vector3 offset = puntaDedo - origen;

        float distanciaPlano = Vector3.Dot(offset, normal);
        if (Mathf.Abs(distanciaPlano) > profundidadToque) return false;

        float proyX = Vector3.Dot(offset, ejeX);
        float proyY = Vector3.Dot(offset, ejeY);

        return proyX >= 0f && proyX <= largoX && proyY >= 0f && proyY <= largoY;
    }

    IEnumerator FlashFeedback(BotonMenuFeedback feedback)
    {
        feedback.ActivarFeedbackManual();
        yield return new WaitForSeconds(duracionFeedback);
        feedback.DesactivarFeedbackManual();
    }
}
