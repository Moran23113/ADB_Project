using System.Text;

/// <summary>
/// Builder simple para componer diagramas Mermaid sin concatenar manualmente cadenas.
/// </summary>
public class MermaidBuilder
{
    private readonly StringBuilder _sb = new();

    /// <summary>Agrega una línea raw al diagrama.</summary>
    public MermaidBuilder AddRaw(string line)
    {
        _sb.AppendLine(line);
        return this;
    }

    /// <summary>Agrega una entidad (nodo) al diagrama.</summary>
    public MermaidBuilder AddEntity(string id, string label, string? classes = null)
    {
        var cls = string.IsNullOrEmpty(classes) ? string.Empty : $":::{classes}";
        _sb.AppendLine($"  {id}[{label}]{cls}");
        return this;
    }

    /// <summary>Agrega una relación o flecha entre nodos.</summary>
    public MermaidBuilder AddRelationship(string from, string to, string connection, string? label = null)
    {
        var txt = string.IsNullOrEmpty(label) ? string.Empty : $" \"{label}\"";
        _sb.AppendLine($"  {from} {connection} {to}{txt}");
        return this;
    }

    /// <summary>Devuelve el Mermaid generado.</summary>
    public string Build() => _sb.ToString();
}
