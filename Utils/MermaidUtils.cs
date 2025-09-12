using System.Text.RegularExpressions;

/// <summary>
/// Utilidades compartidas para generar diagramas Mermaid.
/// </summary>
public static class MermaidUtils
{
    // Regex para sanear identificadores Mermaid.
    private static readonly Regex _idBad = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

    /// <summary>
    /// Sanitiza un identificador para que sea válido en Mermaid.
    /// Reemplaza caracteres no permitidos, asegura inicio con letra
    /// y limita su longitud a 60 caracteres.
    /// </summary>
    public static string SanitizeId(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0]))
            s = "N_" + s;
        if (s.Length > 60) s = s[..60];
        return s;
    }

    /// <summary>
    /// Escapa texto para que no rompa el parser de Mermaid.
    /// Maneja caracteres problemáticos comunes y entidades HTML.
    /// </summary>
    public static string EscapeText(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return "";
        return txt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("[", "&#91;")
            .Replace("]", "&#93;");
    }
}
