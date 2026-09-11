using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú de pausa diegético — una sola clase reutilizada por SceneSimulador y
/// SceneDiagnostico. Lo que cambia por escena es la lista <see cref="items"/>
/// configurada en el Inspector de cada instancia, y a qué UnityEvent apuntan
/// <see cref="onAbrirMenu"/>/<see cref="onCerrarMenu"/> (cada escena decide
/// qué flag de pausa tocar: LogicaRCP+CalibradorPosicion, o
/// GestorCasoClinico). Este gestor no conoce ninguno de esos dos módulos más
/// allá de esos dos UnityEvent.
///
/// Usa el mismo mecanismo de selección diegética que el resto del proyecto
/// (SeleccionableToque + GestorSeleccionToque), con capas para congelar el
/// contenido subyacente mientras el menú o su confirmación están abiertos:
///   Capa 0 = contenido normal de la escena + ícono de pausa
///   Capa 1 = panel de pausa
///   Capa 2 = sub-panel de confirmación
/// </summary>
public class GestorMenuPausa : MonoBehaviour
{
    [System.Serializable]
    public class ItemMenuPausa
    {
        [Tooltip("Etiqueta fija. Ignorada si 'proveedorEtiqueta' está asignado.")]
        public string etiqueta;

        [Tooltip("Opcional: componente que implementa IEtiquetaDinamica (p. ej. BotonMaestro). " +
                 "Se consulta al abrir el menú, no por frame.")]
        public MonoBehaviour proveedorEtiqueta;

        [Tooltip("Opcional: componente que implementa IItemMenuCondicional. Si está asignado " +
                 "y devuelve false, el ítem se oculta del menú. Se consulta al abrir el menú, " +
                 "no por frame. Vacío = ítem siempre visible.")]
        public MonoBehaviour proveedorCondicion;

        [Tooltip("Acción a ejecutar al seleccionar este ítem. Vacía = solo cierra el menú (Reanudar).")]
        public UnityEvent accion;

        [Header("Confirmación (acciones destructivas)")]
        public bool requiereConfirmacion;
        [TextArea] public string tituloConfirmacion;
        [TextArea] public string mensajeConfirmacion;

        public string ObtenerEtiqueta()
        {
            if (proveedorEtiqueta is IEtiquetaDinamica dinamica) return dinamica.ObtenerEtiqueta();
            return etiqueta;
        }

        public bool DebeMostrarse()
        {
            return !(proveedorCondicion is IItemMenuCondicional cond) || cond.DebeMostrarseEnMenu();
        }
    }

    [Header("Contenido (configurable por escena)")]
    [SerializeField] private List<ItemMenuPausa> items = new List<ItemMenuPausa>();

    [Header("Ícono head-locked")]
    [Tooltip("Raíz visual del ícono (con AnclajeCabeza). Se oculta mientras el menú está abierto.")]
    [SerializeField] private GameObject iconoPausa;
    [SerializeField] private SeleccionableToque toqueIcono;

    [Header("Panel de pausa")]
    [SerializeField] private GameObject panelPausa;
    [SerializeField] private Transform contenedorItems;
    [Tooltip("Prefab con RectTransform + Image (pill_button) + TMP_Text + BotonMenuFeedback + SeleccionableToque (Capa 1).")]
    [SerializeField] private GameObject prefabItemMenu;

    [Header("Sub-panel de confirmación")]
    [SerializeField] private GameObject panelConfirmacion;
    [SerializeField] private TMP_Text textoTituloConfirmacion;
    [SerializeField] private TMP_Text textoMensajeConfirmacion;
    [Tooltip("SeleccionableToque en Capa 2.")]
    [SerializeField] private SeleccionableToque botonConfirmar;
    [Tooltip("SeleccionableToque en Capa 2.")]
    [SerializeField] private SeleccionableToque botonCancelar;

    [Header("Eventos de estado (la escena los conecta al flag de pausa)")]
    public UnityEvent onAbrirMenu;
    public UnityEvent onCerrarMenu;

    /// <summary>Eventos C# equivalentes a onAbrirMenu/onCerrarMenu, para código que
    /// se suscribe en runtime en vez de vía Inspector (p. ej. PuenteVisibilidadPausa
    /// ocultando/restaurando los paneles del módulo clínico). No reemplazan a los
    /// UnityEvent existentes — se disparan además de ellos, mismo momento.</summary>
    public event System.Action AlAbrirMenu;
    public event System.Action AlCerrarMenu;

    [Header("Disponibilidad")]
    [Tooltip("Si false, el ícono de pausa se oculta y no se puede abrir el menú. " +
             "La escena lo controla: RCP lo habilita al salir de calibración; " +
             "Razonamiento Clínico, al pasar de la selección de submodo.")]
    [SerializeField] private bool pausaDisponible = true;

    /// <summary>true mientras el panel de pausa (o su confirmación) está abierto.</summary>
    public bool MenuAbierto => panelPausa != null && panelPausa.activeSelf;

    private readonly List<GameObject> _itemsInstanciados = new List<GameObject>();
    private ItemMenuPausa _itemPendiente;

    void Awake()
    {
        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
        ConstruirItems();
        ActualizarIcono();
    }

    /// <summary>La escena habilita/inhabilita la pausa según su propio flujo.
    /// Al inhabilitar, si el menú estaba abierto lo cierra.</summary>
    public void HabilitarPausa(bool disponible)
    {
        if (pausaDisponible == disponible) return;
        pausaDisponible = disponible;
        if (!disponible && MenuAbierto) CerrarMenu();
        else ActualizarIcono();
    }

    private void ActualizarIcono()
    {
        if (iconoPausa != null) iconoPausa.SetActive(pausaDisponible && !MenuAbierto);
    }

    void OnEnable()
    {
        if (toqueIcono != null) toqueIcono.OnSeleccionado += AbrirMenu;
        if (botonConfirmar != null) botonConfirmar.OnSeleccionado += ConfirmarPendiente;
        if (botonCancelar != null) botonCancelar.OnSeleccionado += CancelarConfirmacion;
    }

    void OnDisable()
    {
        if (toqueIcono != null) toqueIcono.OnSeleccionado -= AbrirMenu;
        if (botonConfirmar != null) botonConfirmar.OnSeleccionado -= ConfirmarPendiente;
        if (botonCancelar != null) botonCancelar.OnSeleccionado -= CancelarConfirmacion;
    }

    // ── Construcción ─────────────────────────────────────────────────────────

    private void ConstruirItems()
    {
        if (contenedorItems == null || prefabItemMenu == null) return;

        foreach (var item in items)
        {
            GameObject go = Instantiate(prefabItemMenu, contenedorItems);
            go.SetActive(true);

            var toque = go.GetComponentInChildren<SeleccionableToque>();
            var texto = go.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = item.ObtenerEtiqueta();
            if (toque != null) toque.OnSeleccionado += () => SeleccionarItem(item);

            _itemsInstanciados.Add(go);
        }
    }

    /// <summary>Actualiza etiqueta y visibilidad de cada ítem instanciado según su
    /// proveedor. Se llama al abrir el menú (los proveedores se consultan ahí, no
    /// por frame).</summary>
    private void RefrescarItems()
    {
        for (int i = 0; i < items.Count && i < _itemsInstanciados.Count; i++)
        {
            var go = _itemsInstanciados[i];

            bool visible = items[i].DebeMostrarse();
            if (go.activeSelf != visible) go.SetActive(visible);
            if (!visible) continue;

            var texto = go.GetComponentInChildren<TMP_Text>();
            if (texto != null) texto.text = items[i].ObtenerEtiqueta();
        }
    }

    // ── Abrir / cerrar ───────────────────────────────────────────────────────

    public void AbrirMenu()
    {
        if (!pausaDisponible) return;
        RefrescarItems();
        if (iconoPausa != null) iconoPausa.SetActive(false);
        if (panelPausa != null) panelPausa.SetActive(true);
        GestorSeleccionToque.Instancia?.EstablecerCapa(1);
        onAbrirMenu?.Invoke();
        AlAbrirMenu?.Invoke();
    }

    public void CerrarMenu()
    {
        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
        ActualizarIcono();
        GestorSeleccionToque.Instancia?.EstablecerCapa(0);
        onCerrarMenu?.Invoke();
        AlCerrarMenu?.Invoke();
    }

    // ── Selección de ítems ───────────────────────────────────────────────────

    private void SeleccionarItem(ItemMenuPausa item)
    {
        if (item.requiereConfirmacion)
        {
            _itemPendiente = item;
            if (textoTituloConfirmacion != null) textoTituloConfirmacion.text = item.tituloConfirmacion;
            if (textoMensajeConfirmacion != null) textoMensajeConfirmacion.text = item.mensajeConfirmacion;
            if (panelConfirmacion != null) panelConfirmacion.SetActive(true);
            GestorSeleccionToque.Instancia?.EstablecerCapa(2);
        }
        else
        {
            item.accion?.Invoke();
            CerrarMenu();
        }
    }

    private void ConfirmarPendiente()
    {
        _itemPendiente?.accion?.Invoke();
        _itemPendiente = null;
        CerrarMenu();
    }

    private void CancelarConfirmacion()
    {
        _itemPendiente = null;
        if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
        GestorSeleccionToque.Instancia?.EstablecerCapa(1);
    }

    /// <summary>Utilitario compartido por el ítem "volver al menú principal" de
    /// ambos módulos. Usa GestorNavegacionMenu.escenaMenuPrincipal (const) como
    /// única fuente de verdad del nombre de escena — sin duplicarlo por instancia
    /// ni depender de una referencia cruzada de escena (imposible en Unity).</summary>
    public void VolverAlMenuPrincipal() =>
        SceneManager.LoadScene(GestorNavegacionMenu.escenaMenuPrincipal);
}
