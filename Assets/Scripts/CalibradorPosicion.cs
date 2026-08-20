using UnityEngine;
using TMPro;

public class CalibradorPosicion : MonoBehaviour
{
    [Header("--- INTERRUPTOR MAESTRO ---")]
    public bool sistemaActivo = true; // Si es FALSE, este script no hace NADA.

    [Header("Referencias OVR")]
    public Transform manoDerecha;
    public Transform manoIzquierda;
    public Transform referenciaCamara;

    [Header("Objetos a Calibrar")]
    public Transform pacienteCompleto;
    public Transform sensorPecho;

    [Header("Configuración Riel")]
    public float alturaSuelo = 0.1f;
    public float limiteInferior = -0.15f;
    public float limiteSuperior = 0.15f;

    [Header("Visuales")]
    public Renderer rendererCuerpo;
    public Material materialFantasma;
    private Material materialOriginal;

    [Header("Feedback UI")]
    public TextMeshPro textoInstrucciones;

    // ESTADO INTERNO
    private bool modoEdicionSensor = false;

    // Variables internas
    private bool estaPellizcando = false;
    private Transform objetoSeleccionado;
    private Vector3 offsetPosicion;
    private Quaternion offsetRotacion;
    private bool agarrandoIzquierda = false;
    private float distanciaInicialManos;
    private Vector3 escalaInicialPaciente;

    void Start()
    {
        if (rendererCuerpo != null) materialOriginal = rendererCuerpo.material;

        // AUTO-SPAWN
        if (sistemaActivo && referenciaCamara != null)
        {
            Vector3 mirarAlFrente = referenciaCamara.forward;
            mirarAlFrente.y = 0; mirarAlFrente.Normalize();
            Vector3 posicionFrente = referenciaCamara.position + (mirarAlFrente * 0.8f);
            posicionFrente.y = alturaSuelo;

            pacienteCompleto.position = posicionFrente;
            pacienteCompleto.rotation = Quaternion.LookRotation(mirarAlFrente);
        }
        ActualizarTextoModo();
    }

    public void AlternarModo()
    {
        if (!sistemaActivo) return; // Si estamos en RCP, este botón no hace nada
        modoEdicionSensor = !modoEdicionSensor;
        ActualizarTextoModo();
    }

    public bool ObtenerEstadoModo() { return modoEdicionSensor; }

    // Función pública para que el Botón Maestro nos despierte o nos duerma
    public void SetSistemaActivo(bool activo)
    {
        sistemaActivo = activo;
        if (!activo)
        {
            // Al apagar, aseguramos que el cuerpo se vea normal (no fantasma)
            ActivarFantasma(false);
            if (textoInstrucciones) textoInstrucciones.text = ""; // Borrar texto molesto
        }
        else
        {
            ActualizarTextoModo();
        }
    }

    void ActualizarTextoModo()
    {
        if (!sistemaActivo || textoInstrucciones == null) return;
        textoInstrucciones.text = modoEdicionSensor ? "Modo: SENSOR (Deslizar)" : "Modo: CUERPO (Posicionar)";
    }

    void Update()
    {
        // --- AQUÍ ESTÁ EL CAMBIO CLAVE ---
        if (!sistemaActivo) return;
        // ---------------------------------

        bool pinzaDer = EsPellizco(manoDerecha);
        bool pinzaIzq = EsPellizco(manoIzquierda);

        // 1. ESCALADO
        if (pinzaDer && pinzaIzq)
        {
            objetoSeleccionado = pacienteCompleto;
            GestionarEscalado();
            if (textoInstrucciones) textoInstrucciones.text = "↔ ESCALANDO ↔";
            ActivarFantasma(true);
            estaPellizcando = false;
        }
        // 2. MOVIMIENTO (Mano Derecha)
        else if (pinzaDer)
        {
            if (!estaPellizcando)
            {
                if (modoEdicionSensor)
                {
                    objetoSeleccionado = sensorPecho;
                    if (sensorPecho.parent != null)
                        offsetPosicion = sensorPecho.position - manoDerecha.position;
                }
                else
                {
                    objetoSeleccionado = pacienteCompleto;
                    offsetPosicion = Quaternion.Inverse(manoDerecha.rotation) * (objetoSeleccionado.position - manoDerecha.position);
                    offsetRotacion = Quaternion.Inverse(manoDerecha.rotation) * objetoSeleccionado.rotation;
                    ActivarFantasma(true);
                }
                estaPellizcando = true;
            }

            if (objetoSeleccionado != null)
            {
                if (objetoSeleccionado == sensorPecho)
                {
                    Vector3 destinoDeseadoMundo = manoDerecha.position + offsetPosicion;
                    Vector3 destinoLocal = sensorPecho.parent.InverseTransformPoint(destinoDeseadoMundo);
                    destinoLocal.x = 0;
                    destinoLocal.z = 0;
                    destinoLocal.y = Mathf.Clamp(destinoLocal.y, limiteInferior, limiteSuperior);
                    sensorPecho.localPosition = destinoLocal;
                }
                else
                {
                    Vector3 nuevaPos = manoDerecha.position + (manoDerecha.rotation * offsetPosicion);
                    Quaternion nuevaRot = manoDerecha.rotation * offsetRotacion;
                    Vector3 eulerRot = nuevaRot.eulerAngles;
                    nuevaRot = Quaternion.Euler(0, eulerRot.y, 0);
                    objetoSeleccionado.position = nuevaPos;
                    objetoSeleccionado.rotation = nuevaRot;
                }
            }
            agarrandoIzquierda = false;
        }
        else
        {
            if (estaPellizcando) ActivarFantasma(false);
            estaPellizcando = false;
            objetoSeleccionado = null;
            agarrandoIzquierda = false;
        }
    }

    void ActivarFantasma(bool activar)
    {
        if (rendererCuerpo == null || materialFantasma == null) return;
        if (activar && !modoEdicionSensor) rendererCuerpo.material = materialFantasma;
        else rendererCuerpo.material = materialOriginal;
    }

    void GestionarEscalado()
    {
        float distanciaActual = Vector3.Distance(manoDerecha.position, manoIzquierda.position);
        if (!agarrandoIzquierda)
        {
            distanciaInicialManos = distanciaActual;
            escalaInicialPaciente = pacienteCompleto.localScale;
            agarrandoIzquierda = true;
        }
        else
        {
            float factor = distanciaActual / distanciaInicialManos;
            float nuevaEscala = Mathf.Clamp(factor, 0.5f, 3.0f);
            pacienteCompleto.localScale = escalaInicialPaciente * nuevaEscala;
        }
    }

    bool EsPellizco(Transform mano)
    {
        if (mano == null) return false;
        var handState = mano.GetComponent<OVRHand>();
        return handState != null &&
               handState.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
               handState.GetFingerConfidence(OVRHand.HandFinger.Index) == OVRHand.TrackingConfidence.High;
    }
}