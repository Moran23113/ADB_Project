using System.Text.RegularExpressions;

/// <summary>
/// Utilidades compartidas para generar diagramas Mermaid.
/// </summary>
public static class MermaidUtils
{
    // Regex para sanear identificadores Mermaid.
    private static readonly Regex _idBad = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

  
    public static string Sanitizar(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0]))
            s = "N_" + s;
        if (s.Length > 60) s = s[..60];
        return s;
    }

 
    public static string Escapar(string? txt)
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
