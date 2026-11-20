using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Código anónimo del participante del estudio. Clase estática compartida (la usan el panel de introducción de
/// SceneSimulador y, en la Parte B, el de SceneDiagnostico): vive en PlayerPrefs, así que el código escrito en un
/// módulo aparece ya cargado en el otro y sobrevive entre sesiones de la app.
///
/// Normalización: se pasa a mayúsculas, se quitan tildes y diéresis (Á→A, Ñ→N), se descarta todo lo que no sea letra
/// A-Z, número 0-9 o guion «-», y se corta a <see cref="LARGO_MAX"/> caracteres. Un código es válido si, normalizado,
/// no queda vacío. Sin asignaciones por fotograma: todo ocurre solo cuando el usuario confirma el teclado.
/// </summary>
public static class CodigoParticipante
{
    public const string CLAVE_PLAYERPREFS = "codigoParticipante";
    public const int LARGO_MAX = 12;

    /// <summary>Se dispara solo cuando el código guardado CAMBIA de valor (con el nuevo valor normalizado).</summary>
    public static event Action<string> AlCambiar;

    private static readonly StringBuilder _sb = new StringBuilder(LARGO_MAX + 4);

    /// <summary>Código guardado (ya normalizado), o "" si no hay.</summary>
    public static string Actual
    {
        get
        {
            try { return Normalizar(PlayerPrefs.GetString(CLAVE_PLAYERPREFS, "")); }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>Hay un código válido guardado.</summary>
    public static bool HayCodigo => Actual.Length > 0;

    /// <summary>Normaliza un texto escrito por el usuario. Devuelve "" si no queda nada válido.</summary>
    public static string Normalizar(string crudo)
    {
        if (string.IsNullOrEmpty(crudo)) return "";
        _sb.Length = 0;
        for (int i = 0; i < crudo.Length && _sb.Length < LARGO_MAX; i++)
        {
            char c = SinTilde(crudo[i]);
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-') _sb.Append(c);
        }
        return _sb.ToString();
    }

    /// <summary>¿El texto, normalizado, es un código utilizable (no vacío)?</summary>
    public static bool EsValido(string crudo) => Normalizar(crudo).Length > 0;

    /// <summary>
    /// Guarda el código si es válido. <paramref name="normalizado"/> trae el valor normalizado (o "" si el texto no era
    /// válido). Devuelve true si se guardó. No hace nada (y devuelve false) con un texto inválido: el código previo se conserva.
    /// </summary>
    public static bool Guardar(string crudo, out string normalizado)
    {
        normalizado = Normalizar(crudo);
        if (normalizado.Length == 0) return false;
        string previo = Actual;
        try
        {
            PlayerPrefs.SetString(CLAVE_PLAYERPREFS, normalizado);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CODIGO] no se pudo guardar el código: " + e.Message);
            return false;
        }
        if (previo != normalizado) AlCambiar?.Invoke(normalizado);
        return true;
    }

    /// <summary>Borra el código guardado (pruebas).</summary>
    public static void Borrar()
    {
        try { PlayerPrefs.DeleteKey(CLAVE_PLAYERPREFS); PlayerPrefs.Save(); } catch (Exception) { }
    }

    private static char SinTilde(char c)
    {
        switch (c)
        {
            case 'á': case 'à': case 'ä': case 'â': return 'a';
            case 'é': case 'è': case 'ë': case 'ê': return 'e';
            case 'í': case 'ì': case 'ï': case 'î': return 'i';
            case 'ó': case 'ò': case 'ö': case 'ô': return 'o';
            case 'ú': case 'ù': case 'ü': case 'û': return 'u';
            case 'ñ': return 'n';
            case 'Á': case 'À': case 'Ä': case 'Â': return 'A';
            case 'É': case 'È': case 'Ë': case 'Ê': return 'E';
            case 'Í': case 'Ì': case 'Ï': case 'Î': return 'I';
            case 'Ó': case 'Ò': case 'Ö': case 'Ô': return 'O';
            case 'Ú': case 'Ù': case 'Ü': case 'Û': return 'U';
            case 'Ñ': return 'N';
            default: return c;
        }
    }
}
