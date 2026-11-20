using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Escritura de resultados del estudio en CSV dentro de <c>Application.persistentDataPath</c>. Clase estática compartida
/// (RCP aquí; la Parte B añade el CSV de Diagnóstico con las mismas columnas comunes).
///
/// Formato: UTF-8 sin BOM, separador coma, decimales con punto (InvariantCulture), fin de línea CRLF, una fila por
/// sesión, con append; la cabecera se escribe solo si el archivo no existe (o está vacío). Los campos de texto se
/// escapan (comas, comillas y saltos de línea entre comillas dobles; comillas duplicadas).
///
/// Nunca lanza: cualquier error de E/S se captura y se registra con el prefijo [RESULTADOS] (ruta y mensaje); el
/// resultado de la última escritura queda en <see cref="UltimoExito"/>. Solo se llama al terminar o descartar una
/// sesión (evento), nunca por fotograma, así que las asignaciones de strings aquí son aceptables.
/// </summary>
public static class RegistroResultados
{
    public const string ARCHIVO_RCP = "resultados_rcp.csv";

    public const string ESTADO_COMPLETA = "Completa";
    public const string ESTADO_DESCARTADA = "Descartada";

    /// <summary>Columnas comunes a todos los CSV del estudio (las usa también la Parte B).</summary>
    public const string COLUMNAS_COMUNES = "codigoParticipante,fechaHoraIso,versionApp,modulo,dispositivoEntrada,estado";

    public const string CABECERA_RCP = COLUMNAS_COMUNES +
        ",sesionK,sesionesTotales,duracionSeg,intentos,validas,insuficientes,correctas,excesivas," +
        "profundidadMediaCm,frecuenciaMediaCpm,pctIntervalosEnRango,segPosturaIncorrecta,salidasDeZona," +
        "recoilIncompleto,recoilEvaluado,validasConteoHud";

    public const string ARCHIVO_DIAGNOSTICO = "resultados_diagnostico.csv";

    public const string ESTADO_COMPLETO = "Completo";
    public const string ESTADO_ABANDONADO = "Abandonado";

    public const string CABECERA_DIAGNOSTICO = COLUMNAS_COMUNES +
        ",idCaso,submodo,pose,duracionSeg,puntajeTotal,puntajeMaximo," +
        "diagnosticoObtenido,diagnosticoMaximo,manejoObtenido,manejoMaximo,examenesObtenido,examenesMaximo," +
        "anamnesisObtenido,anamnesisMaximo,examenFisicoObtenido,examenFisicoMaximo,penalizacionErrores," +
        "diagnosticoCorrecto,diagnosticoEscrito,manejoEscrito,preguntasClaveReconocidas,intercambiosReconocidos," +
        "preguntasSinReconocer,regionesExaminadas,examenesPedidos,examenesPertinentes,examenesIndiferentes," +
        "examenesNoPertinentes,erroresDetectados";

    /// <summary>Solo para pruebas: si no está vacío, sustituye a persistentDataPath como carpeta de destino.</summary>
    public static string DirectorioPrueba;

    public static string Directorio => string.IsNullOrEmpty(DirectorioPrueba) ? Application.persistentDataPath : DirectorioPrueba;

    /// <summary>Ruta del último intento de escritura.</summary>
    public static string UltimaRuta { get; private set; }
    /// <summary>Mensaje de error del último intento ("" si salió bien).</summary>
    public static string UltimoError { get; private set; } = "";
    /// <summary>¿La última escritura se completó?</summary>
    public static bool UltimoExito { get; private set; }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly StringBuilder _sb = new StringBuilder(512);
    private static readonly UTF8Encoding Utf8SinBom = new UTF8Encoding(false);

    /// <summary>
    /// Añade una fila al CSV <paramref name="archivo"/>; escribe antes la <paramref name="cabecera"/> si el archivo no
    /// existe. Devuelve true si se escribió. Nunca lanza.
    /// </summary>
    public static bool Escribir(string archivo, string cabecera, string fila)
    {
        string ruta = archivo;
        try
        {
            string dir = Directorio;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            ruta = Path.Combine(dir ?? "", archivo);
            UltimaRuta = ruta;

            var info = new FileInfo(ruta);
            bool nuevo = !info.Exists || info.Length == 0;
            _sb.Length = 0;
            if (nuevo) _sb.Append(cabecera).Append("\r\n");
            _sb.Append(fila).Append("\r\n");
            File.AppendAllText(ruta, _sb.ToString(), Utf8SinBom);

            UltimoError = "";
            UltimoExito = true;
            Debug.Log("[RESULTADOS] fila escrita en " + ruta);
            return true;
        }
        catch (Exception e)
        {
            UltimaRuta = ruta;
            UltimoError = e.GetType().Name + ": " + e.Message;
            UltimoExito = false;
            Debug.LogWarning("[RESULTADOS] no se pudo escribir en " + ruta + " | " + UltimoError);
            return false;
        }
    }

    /// <summary>Construye la fila de resultados de una sesión de RCP (sin salto de línea final).</summary>
    public static string FilaRcp(string codigo, string estado, int sesionK, int sesionesTotales,
                                 ResultadoSesionRCP r, string dispositivoEntrada)
    {
        var sb = new StringBuilder(256);
        sb.Append(Campo(string.IsNullOrEmpty(codigo) ? "SIN-CODIGO" : codigo)).Append(',');
        sb.Append(Campo(FechaHoraIso())).Append(',');
        sb.Append(Campo(Application.version)).Append(',');
        sb.Append("RCP,");
        sb.Append(Campo(dispositivoEntrada)).Append(',');
        sb.Append(Campo(estado)).Append(',');
        sb.Append(sesionK.ToString(Inv)).Append(',');
        sb.Append(sesionesTotales.ToString(Inv)).Append(',');
        sb.Append(Num(r.duracionSeg)).Append(',');
        sb.Append(r.intentosTotales.ToString(Inv)).Append(',');
        sb.Append(r.validas.ToString(Inv)).Append(',');
        sb.Append(r.ciclosInsuficientes.ToString(Inv)).Append(',');
        sb.Append(r.ciclosCorrectos.ToString(Inv)).Append(',');
        sb.Append(r.ciclosExcesivos.ToString(Inv)).Append(',');
        sb.Append(Num(r.profundidadMediaCM)).Append(',');
        sb.Append(Num(r.frecuenciaMediaCpm)).Append(',');
        sb.Append(Num(r.PorcentajeIntervalosEnRango)).Append(',');
        sb.Append(Num(r.segundosPosturaIncorrecta)).Append(',');
        sb.Append(r.salidasDeZona.ToString(Inv)).Append(',');
        sb.Append(r.recoilIncompletos.ToString(Inv)).Append(',');
        sb.Append(r.recoilEvaluados.ToString(Inv)).Append(',');
        sb.Append(r.validasConteoHud.ToString(Inv));
        return sb.ToString();
    }

    /// <summary>Datos de una fila del CSV de Diagnóstico. Los campos de puntaje van "vacíos" (null) en una fila Abandonado.</summary>
    public class DatosFilaDiagnostico
    {
        public string codigo, estado, dispositivoEntrada, idCaso, submodo, pose;
        public float duracionSeg;
        public ResultadoCaso resultado;                 // null si el caso se abandonó (no hubo evaluación)
        public string diagnosticoEscrito, manejoEscrito;
        public int preguntasClaveReconocidas, intercambiosReconocidos, preguntasSinReconocer;
        public string regionesExaminadas, examenesPedidos;   // listas separadas por "|"
        public int examenesPertinentes, examenesIndiferentes, examenesNoPertinentes;
        public string erroresDetectados;                // ids (idReferenciaCondicion) separados por "|"
    }

    /// <summary>Construye la fila del CSV de Diagnóstico (sin salto de línea final). Los textos libres se escriben sin
    /// saltos de línea (se reemplazan por espacios) y con el escape CSV habitual.</summary>
    public static string FilaDiagnostico(DatosFilaDiagnostico d)
    {
        var r = d.resultado;
        var sb = new StringBuilder(512);
        sb.Append(Campo(string.IsNullOrEmpty(d.codigo) ? "SIN-CODIGO" : d.codigo)).Append(',');
        sb.Append(Campo(FechaHoraIso())).Append(',');
        sb.Append(Campo(Application.version)).Append(',');
        sb.Append("Diagnostico,");
        sb.Append(Campo(d.dispositivoEntrada)).Append(',');
        sb.Append(Campo(d.estado)).Append(',');
        sb.Append(Campo(d.idCaso)).Append(',');
        sb.Append(Campo(d.submodo)).Append(',');
        sb.Append(Campo(d.pose)).Append(',');
        sb.Append(Num(d.duracionSeg)).Append(',');
        sb.Append(r != null ? r.puntajeTotal.ToString(Inv) : "").Append(',');
        sb.Append(r != null ? r.puntajeMaximo.ToString(Inv) : "").Append(',');
        Componente(sb, r != null ? r.diagnostico : null); Componente(sb, r != null ? r.manejo : null); Componente(sb, r != null ? r.examenes : null);
        Componente(sb, r != null ? r.anamnesis : null); Componente(sb, r != null ? r.examenFisico : null);
        sb.Append(r != null ? Num(r.penalizacionErrores) : "").Append(',');
        sb.Append(r != null ? (r.diagnosticoCorrecto ? "true" : "false") : "").Append(',');
        sb.Append(Campo(UnaLinea(d.diagnosticoEscrito))).Append(',');
        sb.Append(Campo(UnaLinea(d.manejoEscrito))).Append(',');
        sb.Append(d.preguntasClaveReconocidas.ToString(Inv)).Append(',');
        sb.Append(d.intercambiosReconocidos.ToString(Inv)).Append(',');
        sb.Append(d.preguntasSinReconocer.ToString(Inv)).Append(',');
        sb.Append(Campo(d.regionesExaminadas)).Append(',');
        sb.Append(Campo(d.examenesPedidos)).Append(',');
        sb.Append(d.examenesPertinentes.ToString(Inv)).Append(',');
        sb.Append(d.examenesIndiferentes.ToString(Inv)).Append(',');
        sb.Append(d.examenesNoPertinentes.ToString(Inv)).Append(',');
        sb.Append(Campo(d.erroresDetectados));
        return sb.ToString();
    }

    private static void Componente(StringBuilder sb, ComponentePuntaje c)
    {
        sb.Append(c != null ? Num(c.obtenido) : "").Append(',').Append(c != null ? Num(c.maximo) : "").Append(',');
    }

    /// <summary>Texto en una sola línea: saltos de línea y tabulaciones pasan a espacio y se recortan los extremos.</summary>
    public static string UnaLinea(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
    }

    /// <summary>Fecha y hora local con desfase, ISO 8601 (ej. 2026-11-17T13:25:07-03:00).</summary>
    public static string FechaHoraIso() => DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", Inv);

    /// <summary>Número con dos decimales y punto, independiente de la cultura del dispositivo ("" si no es finito).</summary>
    public static string Num(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return "";
        return v.ToString("0.00", Inv);
    }

    /// <summary>Escapa un campo de texto para CSV (comas, comillas y saltos de línea).</summary>
    public static string Campo(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool necesita = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == ',' || c == '"' || c == '\n' || c == '\r') { necesita = true; break; }
        }
        if (!necesita) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
