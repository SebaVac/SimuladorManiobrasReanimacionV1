using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GestorMenuPinch : MonoBehaviour
{
    [Header("Referencias OVR")]
    public OVRHand manoIzquierda;
    public OVRHand manoDerecha;

    [Header("Detección de botón")]
    [Tooltip("Radio de selección alrededor de la punta del dedo (proximidad esférica). " +
             "Calculado para la separación real entre botones adyacentes del HUD actual.")]
    public float radioSeleccion = 0.035f;

    [Header("Feedback")]
    public float duracionFeedback = 0.15f;

    private bool pellizcandoIzqAnterior = false;
    private bool pellizcandoDerAnterior = false;

    void Update()
    {
        ProcesarMano(manoIzquierda, ref pellizcandoIzqAnterior);
        ProcesarMano(manoDerecha, ref pellizcandoDerAnterior);
    }

    void ProcesarMano(OVRHand mano, ref bool pellizcandoAnterior)
    {
        if (mano == null) return;

        bool pellizcando = EsPellizco(mano);

        if (pellizcando && !pellizcandoAnterior)
        {
            Button boton = BuscarBotonCercano(mano.PointerPose.position);
            if (boton != null)
            {
                var feedback = boton.GetComponent<BotonMenuFeedback>();
                if (feedback != null) StartCoroutine(FlashFeedback(feedback));
                boton.onClick.Invoke();
            }
        }

        pellizcandoAnterior = pellizcando;
    }

    bool EsPellizco(OVRHand mano)
    {
        return mano.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
               mano.GetFingerConfidence(OVRHand.HandFinger.Index) == OVRHand.TrackingConfidence.High;
    }

    Button BuscarBotonCercano(Vector3 posMundo)
    {
        Button[] botones = GetComponentsInChildren<Button>(false);
        Button masCercano = null;
        float distMinSqr = radioSeleccion * radioSeleccion;

        foreach (Button b in botones)
        {
            float distSqr = (b.transform.position - posMundo).sqrMagnitude;
            if (distSqr <= distMinSqr)
            {
                distMinSqr = distSqr;
                masCercano = b;
            }
        }
        return masCercano;
    }

    IEnumerator FlashFeedback(BotonMenuFeedback feedback)
    {
        feedback.ActivarFeedbackManual();
        yield return new WaitForSeconds(duracionFeedback);
        feedback.DesactivarFeedbackManual();
    }
}
