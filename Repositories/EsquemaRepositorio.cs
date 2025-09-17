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
/// Representa únicamente el nombre físico de una tabla encontrada en la base restaurada.
/// </summary>
public record InfoTabla(string Nombre);

/// <summary>
/// Describe cada columna de la base y si interviene como PK o candidata única.
/// </summary>
public record InfoColumna(string Tabla, string Nombre, string Tipo, bool EsNulo, bool EsPk, bool EsUnicoCandidato);

/// <summary>
/// Contiene el detalle de cada restricción de clave foránea detectada en el esquema.
/// </summary>
public record InfoLlaveForanea(
    string Nombre, string TablaPadre, string TablaHija,
    string ColumnasPadreCsv, string ColumnasHijaCsv,
    bool HijaEsUnica, bool HijaTodasNoNulas);

public class InstantaneaEsquema
{
    /// <summary>Listado de tablas de usuario presentes en la base.</summary>
    public List<InfoTabla> Tablas { get; } = new();
    /// <summary>Listado completo de columnas con metadatos enriquecidos.</summary>
    public List<InfoColumna> Columnas { get; } = new();
    /// <summary>Información de todas las claves foráneas para reconstruir las relaciones.</summary>
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();
    /// <summary>
    /// Identifica tablas que actúan como puentes en relaciones muchos a muchos para tratarlas de forma especial.
    /// </summary>
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();
}

public interface IEsquemaRepositorio
{
    InstantaneaEsquema Leer(string nombreBd);
}

public class EsquemaRepositorio : IEsquemaRepositorio
{
    private readonly string cadenaMaestra;

    public EsquemaRepositorio(IConfiguration configuracion)
    {
        cadenaMaestra = configuracion.GetConnectionString("SqlMaestra")!;
    }

    /// <summary>
    /// Abre la base restaurada y genera una instantánea del esquema necesario para los diagramas.
    /// </summary>
    public InstantaneaEsquema Leer(string nombreBd)
    {
        var esquema = new InstantaneaEsquema();

        // Se reutiliza la cadena maestra porque SMO permite cambiar de base a través del objeto Server.
        using var conexion = new SqlConnection(cadenaMaestra);
        var servidor = new Server(new ServerConnection(conexion));
        // Se obtiene la referencia a la base restaurada por nombre.
        var bd = servidor.Databases[nombreBd];

        // Recorre cada tabla de usuario (omitiendo objetos del sistema) para cargar columnas y claves foráneas.
        foreach (Table tabla in bd.Tables)
        {
            if (tabla.IsSystemObject)
                continue;

            // Registra el nombre de la tabla en la instantánea.
            esquema.Tablas.Add(new InfoTabla(tabla.Name));
            // Almacena las columnas básicas de la tabla.
            RegistrarColumnas(tabla, esquema);
            // Revisa las relaciones de la tabla con otras.
            AnalizarLlavesForaneas(tabla, esquema);
        }

        return esquema;
    }

    private static void RegistrarColumnas(Table tabla, InstantaneaEsquema esquema)
    {
        // Obtiene un conjunto con los nombres de columnas que tienen índices únicos para marcarlas como candidatos.
        var columnasUnicas = ObtenerColumnasUnicas(tabla);
        foreach (Column columna in tabla.Columns)
        {
            // Agrega cada columna con la metadata que se utiliza luego para etiquetar PK, nulos y únicos.
            esquema.Columnas.Add(new InfoColumna(
                tabla.Name,
                columna.Name,
                columna.DataType.Name,
                columna.Nullable,
                columna.InPrimaryKey,
                columnasUnicas.Contains(columna.Name)));
        }
    }

    private static HashSet<string> ObtenerColumnasUnicas(Table tabla)
    {
        var resultado = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Se recorre cada índice definido en la tabla y se seleccionan únicamente los índices únicos
        // que no corresponden a la clave primaria (ya se marcan por separado).
        foreach (SmoIndex indice in tabla.Indexes)
            if (indice.IsUnique && indice.IndexKeyType != IndexKeyType.DriPrimaryKey)
                foreach (SmoIndexedColumn col in indice.IndexedColumns)
                    resultado.Add(col.Name);
        return resultado;
    }

    private static void AnalizarLlavesForaneas(Table tabla, InstantaneaEsquema esquema)
    {
        int cantidadFks = 0;
        var tablasReferenciadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Obtiene las columnas que forman parte de la clave primaria para usarlas al detectar tablas puente.
        var columnasPk = tabla.Indexes.Cast<SmoIndex>()
            .FirstOrDefault(i => i.IndexKeyType == IndexKeyType.DriPrimaryKey)?
            .IndexedColumns.Cast<SmoIndexedColumn>()
            .Select(ic => ic.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ForeignKey fk in tabla.ForeignKeys)
        {
            cantidadFks++;
            tablasReferenciadas.Add(fk.ReferencedTable);

            var colsPadre = new List<string>();
            var colsHija = new List<string>();
            // Cada columna de la clave foránea se descompone en su columna padre e hija.
            foreach (ForeignKeyColumn columnaFk in fk.Columns)
            {
                colsPadre.Add(columnaFk.ReferencedColumn);
                colsHija.Add(columnaFk.Name);
            }

            // Se determina si la combinación de columnas hijas es única para inferir cardinalidades.
            bool hijaUnica = tabla.Indexes.Cast<SmoIndex>().Any(i =>
                i.IsUnique &&
                string.Join("|", i.IndexedColumns.Cast<SmoIndexedColumn>().Select(ic => ic.Name)) ==
                string.Join("|", colsHija));

            // También se verifica si todas las columnas hijas son NOT NULL para decidir si la participación es total.
            bool hijaNoNula = colsHija.All(cn => !tabla.Columns[cn].Nullable);

            esquema.LlavesForaneas.Add(new InfoLlaveForanea(
                fk.Name,
                fk.ReferencedTable,
                tabla.Name,
                string.Join(", ", colsPadre),
                string.Join(", ", colsHija),
                hijaUnica,
                hijaNoNula));
        }

        // Si la tabla tiene dos claves foráneas y su PK está formada por las mismas columnas, se considera
        // como tabla puente de una relación muchos a muchos y se marca para un renderizado especial.
        if (columnasPk.Count == 2 && cantidadFks >= 2 && tablasReferenciadas.Count == 2)
            esquema.TablasUnionMuchosAMuchos.Add(tabla.Name);
    }
}
