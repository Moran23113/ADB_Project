using System.Text.RegularExpressions;

namespace ABD_Project.Utilidades;

/// <summary>
/// Funciones auxiliares para sanear identificadores y escapar textos
/// al generar diagramas Mermaid.
/// </summary>
public static class TextoMermaid
{
    private static readonly Regex _idBad = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

    /// <summary>
    /// Sanea un identificador para usarlo como nodo en Mermaid.
    /// Reemplaza caracteres no permitidos por "_" y limita a 60.
    /// </summary>
    public static string San(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0])) s = "N_" + s;
        if (s.Length > 60) s = s[..60];
        return s;
    }

    /// <summary>
    /// Escapa texto para no romper el parser de Mermaid.
    /// </summary>
    public static string Esc(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return string.Empty;
        return txt
            .Replace("\\", "\\\\")   // backslash
            .Replace("\"", "\\\"")   // comillas
            .Replace("\r", " ")      // CR
            .Replace("\n", " ")      // LF
            .Replace("{", "\\{")     // llaves
            .Replace("}", "\\}")     // llaves
            .Replace("<", "&lt;")      // signos menor/mayor
            .Replace(">", "&gt;")
            .Replace("[", "&#91;")    // corchetes
            .Replace("]", "&#93;");
    }
}
