using System.Collections.Generic;

namespace ABD_Project.Servicios.Modelos;

/// <summary>
/// Contenedor con toda la instantánea necesaria para construir el diagrama.
/// </summary>
public class InstantaneaEsquema
{
    /// <summary>Tablas del esquema (excluye del sistema).</summary>
    public List<InfoTabla> Tablas { get; } = new();

    /// <summary>Todas las columnas con flags de PK/Único/Nulabilidad.</summary>
    public List<InfoColumna> Columnas { get; } = new();

    /// <summary>Llaves foráneas (agrupadas) con cardinalidad/participación inferida.</summary>
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();

    /// <summary>Nombres de tablas puente (M:N) identificadas por heurística.</summary>
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();

    /// <summary>Entidades débiles (PK incluye alguna FK) detectadas por heurística.</summary>
    public HashSet<string> EntidadesDebiles { get; } = new();
}
