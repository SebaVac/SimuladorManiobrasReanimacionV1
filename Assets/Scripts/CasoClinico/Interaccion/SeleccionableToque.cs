using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca un elemento (tarjeta, botón diegético, ítem de checklist, botón del
/// menú principal o de GestorMenuPausa) como seleccionable por TOQUE: la
/// punta del índice entra en el rectángulo del elemento dentro de una
/// tolerancia de profundidad. Sin gesto de pinch.
///
/// NO contiene lógica de detección: el árbitro central
/// <see cref="GestorSeleccionToque"/> mide el contacto, detecta el flanco de
/// entrada y llama a <see cref="NotificarSeleccion"/> sobre el elemento tocado.
///
/// Feedback visual: reutiliza <see cref="BotonMenuFeedback"/> (opcional) mediante
/// <c>ActivarFeedbackManual()</c> / <c>DesactivarFeedbackManual()</c>.
///
/// Dos vías para reaccionar a la selección:
///   - <see cref="OnSeleccionado"/> (C#): para botones generados en runtime.
///   - <see cref="alSeleccionar"/> (UnityEvent): para botones estáticos por Inspector.
/// </summary>
[DisallowMultipleComponent]
public class SeleccionableToque : MonoBehaviour
{
    [Header("Zona de toque")]
    [Tooltip("Tolerancia de profundidad (m) frente/detrás del plano del elemento. " +
             "Fuera de esta distancia perpendicular no cuenta como toque.")]
    [SerializeField] private float profundidadToque = 0.05f;

    [Header("Capa de interacción")]
    [Tooltip("Solo se evalúa cuando coincide con GestorSeleccionToque.CapaActiva. " +
             "0 = contenido normal de la escena. Se usa para congelar la interacción " +
             "subyacente cuando se abre un menú modal (p. ej. GestorMenuPausa): sus " +
             "propios botones viven en una capa superior (1, 2, ...) y el resto de la " +
             "escena, en capa 0, deja de responder mientras esa capa esté activa.")]
    [SerializeField] private int capa = 0;
    public int Capa => capa;

    [Header("Feedback visual (opcional)")]
    [SerializeField] private BotonMenuFeedback feedbackVisual;
    [Tooltip("Duración del destello al confirmar una selección, en segundos.")]
    [SerializeField] private float duracionFlashSeleccion = 0.15f;

    [Header("Anti rebote")]
    [Tooltip("Segundos que se ignoran nuevas selecciones tras una selección.")]
    [SerializeField] private float cooldownSeleccionSeg = 0.35f;

    [Header("Evento (Inspector)")]
    [Tooltip("Se invoca al seleccionar. Para botones estáticos (Comenzar, Volver al menú...).")]
    [SerializeField] private UnityEvent alSeleccionar;

    /// <summary>Evento para código. Los paneles que generan botones dinámicamente
    /// se suscriben aquí (alternativas de anamnesis, exámenes, etc.).</summary>
    public event Action OnSeleccionado;

    /// <summary>El panel puede desactivarlo temporalmente (p. ej. mientras muestra
    /// la respuesta del paciente).</summary>
    public bool Interactuable { get; set; } = true;

    /// <summary>true mientras una punta de índice está tocando este elemento.
    /// Lo fija <see cref="GestorSeleccionToque"/>.</summary>
    public bool EnContacto { get; private set; }

    internal bool PuedeSeleccionar =>
        Interactuable && isActiveAndEnabled &&
        Time.time - _tiempoUltimaSeleccion >= cooldownSeleccionSeg;

    /// <summary>true durante los primeros ms tras habilitarse este elemento
    /// (panel que aparece, cambio de escena). El árbitro no lo selecciona en esa
    /// ventana aunque el dedo ya esté encima — hay que sacar el dedo y volver a
    /// entrar. Evita el auto-toque cuando un panel aparece bajo el dedo.</summary>
    internal bool EnGraciaInicio(float graciaSeg) => Time.time - _tiempoHabilitado < graciaSeg;

    private float _tiempoUltimaSeleccion = -999f;
    private float _tiempoHabilitado = -999f;
    private Coroutine _flashActivo;
    private RectTransform _rt;
    private readonly Vector3[] _esquinas = new Vector3[4];

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void Awake() => _rt = transform as RectTransform;

    void OnEnable()
    {
        EnContacto = false;
        _tiempoHabilitado = Time.time;
        if (GestorSeleccionToque.Instancia != null)
            GestorSeleccionToque.Instancia.Registrar(this);
    }

    void OnDisable()
    {
        if (GestorSeleccionToque.Instancia != null)
            GestorSeleccionToque.Instancia.Desregistrar(this);

        if (_flashActivo != null) { StopCoroutine(_flashActivo); _flashActivo = null; }
        if (feedbackVisual != null) feedbackVisual.DesactivarFeedbackManual();
        EnContacto = false;
    }

    // ── Llamados por GestorSeleccionToque ────────────────────────────────────

    /// <summary>Marco del RectTransform en mundo a partir de sus esquinas (reutiliza <c>_esquinas</c>): origen
    /// (abajo-izquierda), ejes X/Y unitarios, largos y normal. Compartido por el toque con dedo y por el rayo
    /// de los controles. Cero-alloc.</summary>
    private bool CalcularMarco(out Vector3 origen, out Vector3 ejeX, out Vector3 ejeY,
                               out float largoX, out float largoY, out Vector3 normal)
    {
        origen = default; ejeX = default; ejeY = default; normal = default; largoX = 0f; largoY = 0f;
        if (_rt == null) return false;

        _rt.GetWorldCorners(_esquinas); // 0=abajo-izq 1=arriba-izq 2=arriba-der 3=abajo-der

        origen = _esquinas[0];
        ejeX = _esquinas[3] - _esquinas[0];
        ejeY = _esquinas[1] - _esquinas[0];
        largoX = ejeX.magnitude;
        largoY = ejeY.magnitude;
        if (largoX <= 0f || largoY <= 0f) return false;
        ejeX /= largoX;
        ejeY /= largoY;
        normal = Vector3.Cross(ejeX, ejeY).normalized;
        return true;
    }

    /// <summary>true si la punta de dedo dada está dentro del rectángulo de este
    /// elemento y dentro de la tolerancia de profundidad. Cero-alloc
    /// (<c>_esquinas</c> reutilizado).</summary>
    internal bool PuntoDentro(Vector3 puntaDedo)
    {
        if (!CalcularMarco(out Vector3 origen, out Vector3 ejeX, out Vector3 ejeY,
                           out float largoX, out float largoY, out Vector3 normal)) return false;

        Vector3 offset = puntaDedo - origen;
        if (Mathf.Abs(Vector3.Dot(offset, normal)) > profundidadToque) return false;

        float proyX = Vector3.Dot(offset, ejeX);
        float proyY = Vector3.Dot(offset, ejeY);
        return proyX >= 0f && proyX <= largoX && proyY >= 0f && proyY <= largoY;
    }

    /// <summary>Intersección de un rayo (controles Touch) con el rectángulo de este elemento, con la MISMA
    /// proyección sobre las esquinas que <see cref="PuntoDentro"/>. Devuelve la distancia a lo largo del
    /// rayo (0 &lt; t &lt;= alcance). Cero-alloc.</summary>
    internal bool IntersectaRayo(Vector3 origenRayo, Vector3 direccion, float alcance, out float distancia)
    {
        distancia = 0f;
        if (!CalcularMarco(out Vector3 origen, out Vector3 ejeX, out Vector3 ejeY,
                           out float largoX, out float largoY, out Vector3 normal)) return false;

        float denom = Vector3.Dot(direccion, normal);
        if (Mathf.Abs(denom) < 1e-4f) return false;                 // rayo paralelo al panel
        float t = Vector3.Dot(origen - origenRayo, normal) / denom;
        if (t <= 0f || t > alcance) return false;

        Vector3 offset = origenRayo + direccion * t - origen;
        float proyX = Vector3.Dot(offset, ejeX);
        float proyY = Vector3.Dot(offset, ejeY);
        if (proyX < 0f || proyX > largoX || proyY < 0f || proyY > largoY) return false;
        distancia = t;
        return true;
    }

    internal void FijarContacto(bool enContacto)
    {
        if (enContacto == EnContacto) return;
        EnContacto = enContacto;

        if (feedbackVisual == null) return;
        if (_flashActivo != null) return; // no pisar un destello en curso
        if (enContacto) feedbackVisual.ActivarFeedbackManual();
        else            feedbackVisual.DesactivarFeedbackManual();
    }

    internal void NotificarSeleccion()
    {
        if (!PuedeSeleccionar) return;
        _tiempoUltimaSeleccion = Time.time;

        OnSeleccionado?.Invoke();
        alSeleccionar?.Invoke();

        if (feedbackVisual != null && isActiveAndEnabled)
        {
            if (_flashActivo != null) StopCoroutine(_flashActivo);
            _flashActivo = StartCoroutine(FlashSeleccion());
        }
    }

    private IEnumerator FlashSeleccion()
    {
        feedbackVisual.ActivarFeedbackManual();
        yield return new WaitForSeconds(duracionFlashSeleccion);
        if (!EnContacto) feedbackVisual.DesactivarFeedbackManual();
        _flashActivo = null;
    }
}
