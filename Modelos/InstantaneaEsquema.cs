namespace ABD_Project.Modelos;

/// <summary>
/// Contenedor con toda la información necesaria para construir los diagramas.
/// </summary>
public class InstantaneaEsquema
{
    /// <summary>Tablas del esquema (excluye las del sistema).</summary>
    public List<InfoTabla> Tablas { get; } = new();

    /// <summary>Todas las columnas con banderas de PK/Único/Nulabilidad.</summary>
    public List<InfoColumna> Columnas { get; } = new();

    /// <summary>Llaves foráneas agrupadas con cardinalidad y participación inferidas.</summary>
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();

    /// <summary>Nombres de tablas puente (M:N) identificadas por heurística.</summary>
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();

    /// <summary>Entidades débiles (PK incluye alguna FK) detectadas por heurística.</summary>
    public HashSet<string> EntidadesDebiles { get; } = new();
}
