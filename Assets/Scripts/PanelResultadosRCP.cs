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

    /// <summary>Semáforo de cada fila tras el último <see cref="Poblar"/> (solo lectura/verificación).</summary>
    public EstadoSemaforoRCP[] Estados => _estados;
    public bool Visible => _visible;

    void OnEnable()
    {
        if (sesion != null) sesion.AlCambiarEstado += AlCambiarEstadoSesion;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  += OcultarTemporalmente;
            menuPausa.AlCerrarMenu += RestaurarVisibilidad;
        }
        if (botonRepetir != null) botonRepetir.OnSeleccionado += RepetirSesion;
        if (botonVolver != null) botonVolver.OnSeleccionado += VolverACalibracion;
        Aplicar();
    }

    void OnDisable()
    {
        if (sesion != null) sesion.AlCambiarEstado -= AlCambiarEstadoSesion;
        if (menuPausa != null)
        {
            menuPausa.AlAbrirMenu  -= OcultarTemporalmente;
            menuPausa.AlCerrarMenu -= RestaurarVisibilidad;
        }
        if (botonRepetir != null) botonRepetir.OnSeleccionado -= RepetirSesion;
        if (botonVolver != null) botonVolver.OnSeleccionado -= VolverACalibracion;
    }

    private void AlCambiarEstadoSesion(EstadoSesionRCP estado)
    {
        if (estado == EstadoSesionRCP.Terminada && sesion.UltimoResultado != null)
            Poblar(sesion.UltimoResultado);
        Aplicar();
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
        if (logica != null) logica.ResetearEstadoSimulacion();
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
        if (textoDuracion != null) textoDuracion.text = "Duración de la sesión: " + FormatoTiempo(r.duracionSeg);

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
