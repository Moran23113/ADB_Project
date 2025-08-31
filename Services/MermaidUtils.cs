using System.Text;

/// <summary>
/// Utilidades comunes para construir diagramas Mermaid.
/// </summary>
public static class MermaidUtils
{
    /// <summary>Sanitiza identificadores para que sean válidos en Mermaid.</summary>
    public static string SanitizeId(string? id)
    {
        var src = string.IsNullOrEmpty(id) ? "X" : id;
        var sb = new StringBuilder(src.Length);
        foreach (var ch in src)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        var s = sb.ToString();
        if (s.Length == 0 || !char.IsLetter(s[0])) s = "N_" + s;
        return s.Length > 60 ? s[..60] : s;
    }

    /// <summary>Escapa texto para evitar romper el parser de Mermaid.</summary>
    public static string EscapeText(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return "";
        return txt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("{", "\\{").Replace("}", "\\}")
            .Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("[", "&#91;").Replace("]", "&#93;");
    }
}
