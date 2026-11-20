using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EstadoSemaforoRCP { SinDatos, Verde, Amarillo, Rojo }

/// <summary>
/// Panel de resultados de desempeño de la sesión cronometrada de RCP (cinco componentes del
/// informe, Sección 7.1; sin puntaje total). Aparece cuando <see cref="SesionRCP"/> pasa a
/// Terminada y se oculta si la sesión se descarta. Se oculta/restaura con el menú de pausa
/// recordando su estado real (<see cref="IPanelOcultable"/>, suscripción directa a
/// <see cref="GestorMenuPausa"/>; en SceneSimulador no existe PuenteVisibilidadPausa).
///
/// Solo lee el <see cref="ResultadoSesionRCP"/>; los umbrales del semáforo son PROVISIONALES
/// (campos serializados, pendientes de justificar en el informe).
///
/// PROTOCOLO DE ESTUDIO (<see cref="modoProtocolo"/>, defecto true): el participante hace
/// <see cref="sesionesPorParticipante"/> sesiones seguidas. El panel muestra «Sesión k de N» y el código; el botón principal
/// es «Siguiente sesión» (equivale a Repetir sesión, sin recalibrar) y en la última «Finalizar» (cierre con «Volver al
/// menú principal»); «Volver a calibración» no aparece (queda el ítem de pausa «Cambiar de modo»). Al terminar una
/// sesión se escribe UNA fila (estado Completa) en resultados_rcp.csv (<see cref="RegistroResultados"/>); una sesión
/// descartada con la sesión en curso (reinicio, cambio de modo o «Salir al menú principal») escribe una fila Descartada
/// y NO avanza k. Con modoProtocolo en false el comportamiento es el de siempre, sin guardar nada.
/// </summary>
public class PanelResultadosRCP : MonoBehaviour, IPanelOcultable
{
    public const int FILAS = 5; // compresiones, profundidad, frecuencia, postura y zona, receso

    [Header("Referencias")]
    [SerializeField] private SesionRCP sesion;
    [SerializeField] private LogicaRCP logica;
    [SerializeField] private BotonMaestro botonMaestro;
    [SerializeField] private GestorMenuPausa menuPausa;
    [Tooltip("Hijo que se activa/desactiva (el controlador queda siempre activo).")]
    [SerializeField] private GameObject raiz;

    [Header("UI")]
    [SerializeField] private TMP_Text textoDuracion;
    [SerializeField] private TMP_Text[] valores = new TMP_Text[FILAS];
    [SerializeField] private Image[] indicadores = new Image[FILAS];
    [SerializeField] private TMP_Text[] etiquetasIndicador = new TMP_Text[FILAS];
    [SerializeField] private SeleccionableToque botonRepetir;
    [SerializeField] private SeleccionableToque botonVolver;

    [Header("Protocolo de estudio")]
    [Tooltip("true: sesiones seguidas por participante (Siguiente sesión / Finalizar), código en el panel y guardado de resultados en CSV. " +
             "false: comportamiento anterior (Repetir sesión / Volver a calibración), sin guardar nada.")]
    [SerializeField] private bool modoProtocolo = true;
    [Tooltip("Sesiones que hace cada participante antes de «Finalizar» (1 a 5).")]
    [Range(1, 5)] [SerializeField] private int sesionesPorParticipante = 3;
    [Tooltip("Etiqueta del botón principal (Repetir sesión / Siguiente sesión / Finalizar).")]
    [SerializeField] private TMP_Text textoEtiquetaRepetir;
    [Tooltip("Línea discreta «Resultados guardados» / «No se pudieron guardar».")]
    [SerializeField] private TMP_Text textoGuardado;
    [Tooltip("Cierre tras la última sesión (hijo del canvas de resultados, tapa el contenido).")]
    [SerializeField] private GameObject cierre;
    [SerializeField] private TMP_Text textoCierre;
    [SerializeField] private SeleccionableToque botonMenuPrincipal;

    [Header("Semáforo — PROVISIONAL (pendiente de justificar en el informe)")]
    [Tooltip("Porcentaje mínimo para verde (compresiones válidas, profundidad correcta, postura correcta, recoil completo).")]
    [SerializeField] private float porcentajeVerdeMin = 80f;
    [Tooltip("Porcentaje mínimo para amarillo; por debajo, rojo.")]
    [SerializeField] private float porcentajeAmarilloMin = 60f;
    [Tooltip("Frecuencia media en este rango = verde.")]
    [SerializeField] private float cpmVerdeMin = 100f;
    [SerializeField] private float cpmVerdeMax = 120f;
    [Tooltip("Hasta esta distancia (cpm) fuera del rango verde = amarillo; más allá, rojo.")]
    [SerializeField] private float cpmToleranciaAmarillo = 10f;
    [Tooltip("Salidas de zona: hasta este número = verde.")]
    [SerializeField] private int salidasVerdeMax = 2;
    [Tooltip("Salidas de zona: hasta este número = amarillo; más, rojo.")]
    [SerializeField] private int salidasAmarilloMax = 5;

    [Header("Colores (los tres del proyecto, como GestorMetricasRCP)")]
    [SerializeField] private Color colorCorrecto = new Color(0.15f, 0.85f, 0.35f);
    [SerializeField] private Color colorAdvertencia = new Color(0.95f, 0.85f, 0.15f);
    [SerializeField] private Color colorPeligro = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color colorSinDatos = new Color(0.45f, 0.52f, 0.55f);

    private readonly EstadoSemaforoRCP[] _estados = new EstadoSemaforoRCP[FILAS];
    private bool _ocultoPorPausa;
    private bool _visible;

    // Protocolo
    private int _k = 1;                       // número de la sesión en curso (1..N)
    private bool _cierreVisible;
    private bool _guardadoEstaSesion;          // ya se escribió la fila Completa de la sesión actual (evita duplicados)
    private bool _ultimoGuardadoOk;
    private bool _cerrandoApp;
    private Vector2 _posRepetirOriginal, _posVolverOriginal;
    private bool _posicionesLeidas;
    private static readonly Color ColorGuardadoOk = new Color(0.62f, 0.74f, 0.78f, 1f);
    private static readonly Color ColorGuardadoError = new Color(0.95f, 0.85f, 0.15f, 1f);
    public const string DISPOSITIVO_ENTRADA = "Manos"; // SceneSimulador solo usa seguimiento de manos

    public bool ModoProtocolo => modoProtocolo;
    public int SesionesPorParticipante => Mathf.Clamp(sesionesPorParticipante, 1, 5);
    /// <summary>Número (1..N) de la sesión en curso o, con la sesión terminada, de la que se está mostrando.</summary>
    public int SesionActual => _k;
    public bool CierreVisible => _cierreVisible;
    public bool UltimoGuardadoOk => _ultimoGuardadoOk;

    void OnValidate() { sesionesPorParticipante = Mathf.Clamp(sesionesPorParticipante, 1, 5); }

    /// <summary>Semáforo de cada fila tras el último <see cref="Poblar"/> (solo lectura/verificación).</summary>
    public EstadoSemaforoRCP[] Estados => _estados;
    public bool Visible => _visible;

    void OnEnable()
    {
        if (sesion != null)
        {
            sesion.AlCambiarEstado += AlCambiarEstadoSesion;
            sesion.AlDescartar     += AlDescartarSesion;
        }
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += OcultarTemporalmente;
            menuPausa.AlCerrarMenu += RestaurarVisibilidad;
        }
        if (botonRepetir != null) botonRepetir.OnSeleccionado += RepetirSesion;
        if (botonVolver != null) botonVolver.OnSeleccionado += VolverACalibracion;
        if (botonMenuPrincipal != null) botonMenuPrincipal.OnSeleccionado += VolverAlMenuPrincipal;
        CodigoParticipante.AlCambiar += AlCambiarCodigo;
        Application.quitting += AlCerrarAplicacion;
        AplicarModoProtocolo();
        Aplicar();
    }

    void OnDisable()
    {
        // «Salir al menú principal» descarga la escena sin pasar por ResetearEstadoSimulacion: si había una sesión en
        // curso se registra aquí como Descartada (no al cerrar la aplicación ni al detener Play: ahí no hay sesión que guardar).
        // OJO: al descargar la escena Unity puede haber destruido ya SesionRCP (su "== null" es true), pero ConstruirParcial
        // solo lee campos gestionados, así que se compara la referencia, no el nulo de Unity.
        if (modoProtocolo && !_cerrandoApp && (object)sesion != null)
        {
            var parcial = sesion.ConstruirParcial();
            if (parcial != null) Guardar(RegistroResultados.ESTADO_DESCARTADA, parcial);
        }

        if (sesion != null)
        {
            sesion.AlCambiarEstado -= AlCambiarEstadoSesion;
            sesion.AlDescartar     -= AlDescartarSesion;
        }
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= OcultarTemporalmente;
            menuPausa.AlCerrarMenu -= RestaurarVisibilidad;
        }
        if (botonRepetir != null) botonRepetir.OnSeleccionado -= RepetirSesion;
        if (botonVolver != null) botonVolver.OnSeleccionado -= VolverACalibracion;
        if (botonMenuPrincipal != null) botonMenuPrincipal.OnSeleccionado -= VolverAlMenuPrincipal;
        CodigoParticipante.AlCambiar -= AlCambiarCodigo;
        Application.quitting -= AlCerrarAplicacion;
    }

    private void AlCerrarAplicacion() { _cerrandoApp = true; }

    private void AlCambiarEstadoSesion(EstadoSesionRCP estado)
    {
        if (estado == EstadoSesionRCP.Terminada && sesion.UltimoResultado != null)
        {
            // La fila se escribe aquí, UNA vez por sesión terminada (no en Poblar: re-mostrar el panel no duplica).
            if (modoProtocolo && !_guardadoEstaSesion)
            {
                _guardadoEstaSesion = true;
                Guardar(RegistroResultados.ESTADO_COMPLETA, sesion.UltimoResultado);
            }
            Poblar(sesion.UltimoResultado);
        }
        else if (estado != EstadoSesionRCP.Terminada)
        {
            _guardadoEstaSesion = false;
        }
        Aplicar();
    }

    // Sesión descartada (reinicio, cambio de modo o «Siguiente sesión»): con la sesión EN CURSO se registra una fila
    // Descartada y k NO avanza; con la sesión ya TERMINADA el participante pasa a la sesión siguiente (tras la última
    // empieza una ronda nueva en k = 1).
    private void AlDescartarSesion(EstadoSesionRCP previo, ResultadoSesionRCP parcial)
    {
        _cierreVisible = false;
        if (cierre != null && cierre.activeSelf) cierre.SetActive(false);
        _guardadoEstaSesion = false;
        if (!modoProtocolo) return;

        if (previo == EstadoSesionRCP.EnCurso && parcial != null)
        {
            Guardar(RegistroResultados.ESTADO_DESCARTADA, parcial);
        }
        else if (previo == EstadoSesionRCP.Terminada)
        {
            _k = _k < SesionesPorParticipante ? _k + 1 : 1;
        }
        AplicarModoProtocolo();
    }

    private void AlCambiarCodigo(string nuevo)
    {
        _k = 1;                                 // otro participante: vuelve a la sesión 1
        _cierreVisible = false;
        if (cierre != null && cierre.activeSelf) cierre.SetActive(false);
        AplicarModoProtocolo();
    }

    private void Aplicar()
    {
        bool debeVerse = sesion != null && sesion.Estado == EstadoSesionRCP.Terminada && !_ocultoPorPausa;
        _visible = debeVerse;
        if (raiz != null && raiz.activeSelf != debeVerse) raiz.SetActive(debeVerse);
    }

    // ── IPanelOcultable ──────────────────────────────────────────────────────
    // La visibilidad se RECALCULA desde el estado de la sesión: si durante la pausa se descartó
    // (p. ej. "Cambiar a modo Calibración"), al cerrar el menú el panel no reaparece.

    public void OcultarTemporalmente()
    {
        _ocultoPorPausa = true;
        Aplicar();
    }

    public void RestaurarVisibilidad()
    {
        _ocultoPorPausa = false;
        Aplicar();
    }

    // ── Botones ──────────────────────────────────────────────────────────────

    /// <summary>"Repetir sesión": mismo camino que "Reiniciar sesión" del menú de pausa
    /// (LogicaRCP.ResetearEstadoSimulacion). Descarta la sesión y se queda en Simulación,
    /// sin recalibrar.</summary>
    public void RepetirSesion()
    {
        // Protocolo: en la última sesión el botón es «Finalizar» y muestra el cierre en vez de repetir.
        if (modoProtocolo && _k >= SesionesPorParticipante && sesion != null && sesion.Estado == EstadoSesionRCP.Terminada)
        {
            MostrarCierre();
            return;
        }
        if (logica != null) logica.ResetearEstadoSimulacion();
    }

    /// <summary>«Volver al menú principal» del cierre (misma ruta que el ítem de pausa).</summary>
    public void VolverAlMenuPrincipal()
    {
        if (menuPausa != null) menuPausa.VolverAlMenuPrincipal();
    }

    private void MostrarCierre()
    {
        _cierreVisible = true;
        if (textoCierre != null)
            textoCierre.text = "Terminaste las " + SesionesPorParticipante + " sesiones. Gracias.\nTu código: "
                               + (CodigoParticipante.HayCodigo ? CodigoParticipante.Actual : "—");
        if (botonRepetir != null) botonRepetir.gameObject.SetActive(false);   // el cierre tapa el panel: nada que se pueda pulsar por detrás
        if (botonVolver != null) botonVolver.gameObject.SetActive(false);
        if (cierre != null) cierre.SetActive(true);
    }

    private void Guardar(string estado, ResultadoSesionRCP r)
    {
        string fila = RegistroResultados.FilaRcp(CodigoParticipante.Actual, estado, _k, SesionesPorParticipante, r, DISPOSITIVO_ENTRADA);
        _ultimoGuardadoOk = RegistroResultados.Escribir(RegistroResultados.ARCHIVO_RCP, RegistroResultados.CABECERA_RCP, fila);
        if (textoGuardado != null)
        {
            textoGuardado.text = _ultimoGuardadoOk ? "Resultados guardados" : "No se pudieron guardar";
            textoGuardado.color = _ultimoGuardadoOk ? ColorGuardadoOk : ColorGuardadoError;
            textoGuardado.gameObject.SetActive(true);
        }
    }

    // Botón principal, botón «Volver a calibración» y línea de guardado según el modo (y la sesión k de N).
    private void AplicarModoProtocolo()
    {
        if (!_posicionesLeidas)
        {
            if (botonRepetir != null) _posRepetirOriginal = ((RectTransform)botonRepetir.transform).anchoredPosition;
            if (botonVolver != null) _posVolverOriginal = ((RectTransform)botonVolver.transform).anchoredPosition;
            _posicionesLeidas = true;
        }

        if (botonRepetir != null)
        {
            if (!_cierreVisible && !botonRepetir.gameObject.activeSelf) botonRepetir.gameObject.SetActive(true);
            ((RectTransform)botonRepetir.transform).anchoredPosition = modoProtocolo ? new Vector2(0f, _posRepetirOriginal.y) : _posRepetirOriginal;
        }
        if (botonVolver != null)
        {
            bool ver = !modoProtocolo && !_cierreVisible;
            if (botonVolver.gameObject.activeSelf != ver) botonVolver.gameObject.SetActive(ver);
        }
        if (textoEtiquetaRepetir != null)
            textoEtiquetaRepetir.text = !modoProtocolo ? "REPETIR SESIÓN"
                                      : _k < SesionesPorParticipante ? "SIGUIENTE SESIÓN" : "FINALIZAR";
        if (textoGuardado != null && (!modoProtocolo || sesion == null || sesion.Estado != EstadoSesionRCP.Terminada))
            textoGuardado.gameObject.SetActive(false);
    }

    /// <summary>"Volver a calibración": mismo camino que el ítem de cambio de modo del menú de
    /// pausa (BotonMaestro.AlternarModo, que también resetea el estado de simulación).</summary>
    public void VolverACalibracion()
    {
        if (botonMaestro != null && logica != null && !logica.modoCalibracion) botonMaestro.AlternarModo();
    }

    // ── Presentación (una sola vez por sesión: se permiten strings aquí) ────

    public void Poblar(ResultadoSesionRCP r)
    {
        if (textoDuracion != null)
            textoDuracion.text = modoProtocolo
                ? "Sesión " + _k + " de " + SesionesPorParticipante + "  ·  Código: "
                  + (CodigoParticipante.HayCodigo ? CodigoParticipante.Actual : "—") + "  ·  Duración " + FormatoTiempo(r.duracionSeg)
                : "Duración de la sesión: " + FormatoTiempo(r.duracionSeg);
        AplicarModoProtocolo();

        // 1. Compresiones: válidas / totales y %
        _estados[0] = r.intentosTotales <= 0 ? EstadoSemaforoRCP.SinDatos : PorPorcentaje(r.PorcentajeValidas);
        Fila(0, r.intentosTotales <= 0
            ? "Sin compresiones registradas"
            : r.validas + " válidas de " + r.intentosTotales + " intentos\n(" + Pct(r.PorcentajeValidas) + " %)");

        // 2. Profundidad: insuficientes / correctas / excesivas y media
        _estados[1] = r.intentosTotales <= 0 ? EstadoSemaforoRCP.SinDatos : PorPorcentaje(r.PorcentajeCorrectosProfundidad);
        Fila(1, r.intentosTotales <= 0
            ? "Sin datos"
            : "Insuficientes " + r.ciclosInsuficientes + "  ·  Correctas " + r.ciclosCorrectos + "  ·  Excesivas " + r.ciclosExcesivos
              + "\nMedia: " + Dec(r.profundidadMediaCM) + " cm");

        // 3. Frecuencia: media (cpm) y % de intervalos dentro de 100-120/min
        if (r.intervalos <= 0) { _estados[2] = EstadoSemaforoRCP.SinDatos; Fila(2, "Datos insuficientes\n(se necesitan al menos 2 compresiones)"); }
        else
        {
            _estados[2] = PorFrecuencia(r.frecuenciaMediaCpm);
            Fila(2, "Media: " + Mathf.RoundToInt(r.frecuenciaMediaCpm) + " cpm\n"
                    + Pct(r.PorcentajeIntervalosEnRango) + " % de los intervalos entre " + cpmVerdeMin + " y " + cpmVerdeMax + "/min");
        }

        // 4. Postura y zona: tiempo con postura incorrecta y salidas de zona (peor de los dos criterios)
        var ePostura = PorPorcentaje(r.PorcentajePosturaCorrecta);
        var eZona = r.salidasDeZona <= salidasVerdeMax ? EstadoSemaforoRCP.Verde
                  : r.salidasDeZona <= salidasAmarilloMax ? EstadoSemaforoRCP.Amarillo : EstadoSemaforoRCP.Rojo;
        _estados[3] = (EstadoSemaforoRCP)Mathf.Max((int)ePostura, (int)eZona);
        Fila(3, "Postura incorrecta: " + Dec(r.segundosPosturaIncorrecta) + " s\nSalidas de zona: " + r.salidasDeZona);

        // 5. Receso torácico: compresiones con recoil incompleto
        _estados[4] = r.recoilEvaluados <= 0 ? EstadoSemaforoRCP.SinDatos : PorPorcentaje(100f - r.PorcentajeRecoilIncompleto);
        Fila(4, r.recoilEvaluados <= 0
            ? "Sin compresiones evaluadas"
            : "Recoil incompleto: " + r.recoilIncompletos + " de " + r.recoilEvaluados + " compresiones\n(" + Pct(r.PorcentajeRecoilIncompleto) + " %)");

        for (int i = 0; i < FILAS; i++) PintarIndicador(i, _estados[i]);
    }

    private EstadoSemaforoRCP PorPorcentaje(float pct)
    {
        if (pct >= porcentajeVerdeMin) return EstadoSemaforoRCP.Verde;
        if (pct >= porcentajeAmarilloMin) return EstadoSemaforoRCP.Amarillo;
        return EstadoSemaforoRCP.Rojo;
    }

    private EstadoSemaforoRCP PorFrecuencia(float cpm)
    {
        if (cpm >= cpmVerdeMin && cpm <= cpmVerdeMax) return EstadoSemaforoRCP.Verde;
        if (cpm >= cpmVerdeMin - cpmToleranciaAmarillo && cpm <= cpmVerdeMax + cpmToleranciaAmarillo) return EstadoSemaforoRCP.Amarillo;
        return EstadoSemaforoRCP.Rojo;
    }

    private void Fila(int i, string texto)
    {
        if (valores != null && i < valores.Length && valores[i] != null) valores[i].text = texto;
    }

    private void PintarIndicador(int i, EstadoSemaforoRCP e)
    {
        Color c; string t;
        switch (e)
        {
            case EstadoSemaforoRCP.Verde:    c = colorCorrecto;    t = "CORRECTO"; break;
            case EstadoSemaforoRCP.Amarillo: c = colorAdvertencia; t = "INSUFICIENTE"; break;
            case EstadoSemaforoRCP.Rojo:     c = colorPeligro;     t = "INCORRECTO"; break;
            default:                         c = colorSinDatos;    t = "SIN DATOS"; break;
        }
        if (indicadores != null && i < indicadores.Length && indicadores[i] != null) indicadores[i].color = c;
        if (etiquetasIndicador != null && i < etiquetasIndicador.Length && etiquetasIndicador[i] != null)
            etiquetasIndicador[i].text = t;
    }

    private static string FormatoTiempo(float seg)
    {
        int s = Mathf.RoundToInt(seg);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    private static string Pct(float v) => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);

    // Decimal con coma (español), independiente de la cultura del dispositivo.
    private static string Dec(float v) => v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
}
