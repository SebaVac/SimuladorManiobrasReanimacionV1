/// <summary>
/// Resultado de una sesión cronometrada de RCP (cinco componentes del informe, Sección 7.1)
/// más los acumuladores crudos. Lo construye <see cref="SesionRCP"/> UNA sola vez, al terminar
/// la sesión; el panel solo lo lee. Datos puros, sin lógica de semáforo (eso vive en el panel).
/// </summary>
[System.Serializable]
public class ResultadoSesionRCP
{
    /// <summary>Duración configurada de la sesión, en segundos. En un resultado PARCIAL (<see cref="parcial"/>) es el
    /// tiempo efectivamente transcurrido antes de descartar la sesión.</summary>
    public float duracionSeg;

    /// <summary>true si la sesión se descartó antes de terminar (lo construye <see cref="SesionRCP"/> solo para el
    /// registro del estudio; el panel de resultados nunca lo muestra).</summary>
    public bool parcial;

    // a) Compresiones. Un "intento" es un ciclo de push de cualquier profundidad (>2 cm).
    public int intentosTotales;
    /// <summary>Compresiones válidas = ciclos cuya profundidad MÁXIMA quedó en 5,0-6,0 cm.</summary>
    public int validas;
    /// <summary>Conteo "legado" de LogicaRCP (HUD "Total:"). Puede superar a <see cref="validas"/>:
    /// un ciclo excesivo que cruza 5-6 cm camino a su máximo se cuenta ahí como válido.</summary>
    public int validasConteoHud;

    // b) Profundidad (clasificación por profundidad máxima de cada ciclo).
    public int ciclosInsuficientes;   // < 5,0 cm
    public int ciclosCorrectos;       // 5,0 - 6,0 cm
    public int ciclosExcesivos;       // > 6,0 cm
    /// <summary>Media de la profundidad máxima de todos los ciclos, en cm.</summary>
    public float profundidadMediaCM;

    // c) Frecuencia: intervalos entre inicios de ciclos consecutivos.
    public int intervalos;
    public int intervalosEnRango;     // dentro de cpmMinimo-cpmMaximo
    /// <summary>60 / intervalo medio. 0 si hubo menos de 2 compresiones.</summary>
    public float frecuenciaMediaCpm;

    // d) Postura y zona.
    public float segundosPosturaIncorrecta;
    public int salidasDeZona;

    // e) Receso torácico (recoil), sobre las compresiones válidas cuyo recoil llegó a evaluarse.
    public int recoilEvaluados;
    public int recoilIncompletos;

    public float PorcentajeValidas => intentosTotales > 0 ? 100f * validas / intentosTotales : 0f;
    public float PorcentajeCorrectosProfundidad => intentosTotales > 0 ? 100f * ciclosCorrectos / intentosTotales : 0f;
    public float PorcentajeIntervalosEnRango => intervalos > 0 ? 100f * intervalosEnRango / intervalos : 0f;
    public float PorcentajePosturaCorrecta => duracionSeg > 0f ? 100f * (1f - segundosPosturaIncorrecta / duracionSeg) : 0f;
    public float PorcentajeRecoilIncompleto => recoilEvaluados > 0 ? 100f * recoilIncompletos / recoilEvaluados : 0f;
}
