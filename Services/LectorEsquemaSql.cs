using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SmoIndex = Microsoft.SqlServer.Management.Smo.Index;
using SmoIndexedColumn = Microsoft.SqlServer.Management.Smo.IndexedColumn;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Información mínima de una tabla para el diagrama.
/// </summary>
/// <param name="Nombre">Nombre lógico de la tabla.</param>
public record InfoTabla(string Nombre);

/// <summary>
/// Columna con metadatos relevantes para ER/EER.
/// </summary>
/// <param name="Tabla">Nombre de la tabla a la que pertenece.</param>
/// <param name="Nombre">Nombre de la columna.</param>
/// <param name="Tipo">Tipo de datos (user type).</param>
/// <param name="EsNulo">Indica si admite NULL.</param>
/// <param name="EsPk">Indica si forma parte de la clave primaria.</param>
/// <param name="EsUnicoCandidato">Indica si participa en alguna restricción/índice único (no PK).</param>
public record InfoColumna(string Tabla, string Nombre, string Tipo, bool EsNulo, bool EsPk, bool EsUnicoCandidato);

/// <summary>
/// Llave foránea agrupada (posiblemente multicolumna).
/// </summary>
/// <param name="Nombre">Nombre de la FK.</param>
/// <param name="TablaPadre">Tabla referenciada (lado 1).</param>
/// <param name="TablaHija">Tabla que contiene la FK (lado N/0..1).</param>
/// <param name="ColumnasPadreCsv">Columnas PK/UK referenciadas (en orden).</param>
/// <param name="ColumnasHijaCsv">Columnas FK en la hija (en orden).</param>
/// <param name="HijaEsUnica">True si el conjunto de columnas hija está cubierto por un índice único.</param>
/// <param name="HijaTodasNoNulas">True si todas las columnas FK en la hija son NOT NULL.</param>
public record InfoLlaveForanea(
    string Nombre, string TablaPadre, string TablaHija,
    string ColumnasPadreCsv, string ColumnasHijaCsv,
    bool HijaEsUnica, bool HijaTodasNoNulas);

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

/// <summary>
/// Lector de metadatos desde SQL Server usando SMO.
/// Devuelve una <see cref="InstantaneaEsquema"/> lista para alimentar el constructor del diagrama.
/// </summary>
public class LectorEsquemaSql
{
    private readonly string _cnnMaestra;

    /// <summary>
    /// Crea el lector con una cadena de conexión "maestra" (server/credenciales),
    /// desde la cual se apuntará dinámicamente al catálogo de la BD restaurada.
    /// </summary>
    public LectorEsquemaSql(IConfiguration cfg) => _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;

    /// <summary>
    /// Lee el esquema (tablas, columnas, FKs, heurísticas) de la base indicada.
    /// </summary>
    /// <param name="nombreBD">Nombre de la base restaurada.</param>
    /// <returns>Instantánea del esquema para diagrama ER/EER.</returns>
    public InstantaneaEsquema Leer(string nombreBD)
    {
        var s = new InstantaneaEsquema();

        using var sqlConn = new SqlConnection(_cnnMaestra);
        var serverConn = new ServerConnection(sqlConn);
        var server = new Server(serverConn);
        var db = server.Databases[nombreBD];

        foreach (Table t in db.Tables)
        {
            if (t.IsSystemObject)
                continue;

            s.Tablas.Add(new InfoTabla(t.Name));

            // Columnas marcadas como únicas (índices únicos no PK)
            var uniqueCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SmoIndex idx in t.Indexes)
                if (idx.IsUnique && idx.IndexKeyType != IndexKeyType.DriPrimaryKey)
                    foreach (SmoIndexedColumn ic in idx.IndexedColumns)
                        uniqueCols.Add(ic.Name);

            // Información de PK para heurísticas posteriores
            var pk = t.Indexes.Cast<SmoIndex>().FirstOrDefault(i => i.IndexKeyType == IndexKeyType.DriPrimaryKey);
            var pkCols = pk == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(pk.IndexedColumns.Cast<SmoIndexedColumn>().Select(ic => ic.Name), StringComparer.OrdinalIgnoreCase);

            // Registro de columnas con flags PK/UK/Null
            foreach (Column col in t.Columns)
            {
                s.Columnas.Add(new InfoColumna(
                    t.Name,
                    col.Name,
                    col.DataType.Name,
                    col.Nullable,
                    col.InPrimaryKey,
                    uniqueCols.Contains(col.Name)));
            }

            int fkCount = 0;
            var distinctRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool fkInPk = false;

            // Llaves foráneas y cardinalidad/participación
            foreach (ForeignKey fk in t.ForeignKeys)
            {
                fkCount++;
                distinctRef.Add(fk.ReferencedTable);

                var colsPadre = new List<string>();
                var colsHija = new List<string>();
                foreach (ForeignKeyColumn fkc in fk.Columns)
                {
                    colsPadre.Add(fkc.ReferencedColumn);
                    colsHija.Add(fkc.Name);
                    if (pkCols.Contains(fkc.Name)) fkInPk = true; // heurística de entidad débil
                }

                bool hijaUnica = t.Indexes.Cast<SmoIndex>().Any(i =>
                    i.IsUnique &&
                    string.Join("|", i.IndexedColumns.Cast<SmoIndexedColumn>().Select(ic => ic.Name)) ==
                    string.Join("|", colsHija));

                bool allNotNull = colsHija.All(cn => !t.Columns[cn].Nullable);

                s.LlavesForaneas.Add(new InfoLlaveForanea(
                    fk.Name,
                    fk.ReferencedTable,
                    t.Name,
                    string.Join(", ", colsPadre),
                    string.Join(", ", colsHija),
                    hijaUnica,
                    allNotNull));
            }

            // Heurística: tabla puente M:N (PK con 2 columnas, 2 FKs a tablas distintas)
            if (pkCols.Count == 2 && fkCount >= 2 && distinctRef.Count == 2)
                s.TablasUnionMuchosAMuchos.Add(t.Name);

            // Heurística: entidad débil (alguna FK forma parte de la PK)
            if (fkInPk)
                s.EntidadesDebiles.Add(t.Name);
        }

        return s;
    }
}
