using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SmoIndex = Microsoft.SqlServer.Management.Smo.Index;
using SmoIndexedColumn = Microsoft.SqlServer.Management.Smo.IndexedColumn;
using System;
using System.Collections.Generic;
using System.Linq;

public record InfoTabla(string Nombre);

public record InfoColumna(string Tabla, string Nombre, string Tipo, bool EsNulo, bool EsPk, bool EsUnicoCandidato);

public record InfoLlaveForanea(
    string Nombre, string TablaPadre, string TablaHija,
    string ColumnasPadreCsv, string ColumnasHijaCsv,
    bool HijaEsUnica, bool HijaTodasNoNulas);

public class InstantaneaEsquema
{
    public List<InfoTabla> Tablas { get; } = new();
    public List<InfoColumna> Columnas { get; } = new();
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();
    public HashSet<string> EntidadesDebiles { get; } = new();
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

    public InstantaneaEsquema Leer(string nombreBd)
    {
        var esquema = new InstantaneaEsquema();

        using var conexion = new SqlConnection(cadenaMaestra);
        var servidor = new Server(new ServerConnection(conexion));
        var bd = servidor.Databases[nombreBd];

        foreach (Table tabla in bd.Tables)
        {
            if (tabla.IsSystemObject)
                continue;

            esquema.Tablas.Add(new InfoTabla(tabla.Name));
            RegistrarColumnas(tabla, esquema);
            AnalizarLlavesForaneas(tabla, esquema);
        }

        return esquema;
    }

    private static void RegistrarColumnas(Table tabla, InstantaneaEsquema esquema)
    {
        var columnasUnicas = ObtenerColumnasUnicas(tabla);
        foreach (Column columna in tabla.Columns)
        {
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
        bool fkEnPk = false;
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
            foreach (ForeignKeyColumn columnaFk in fk.Columns)
            {
                colsPadre.Add(columnaFk.ReferencedColumn);
                colsHija.Add(columnaFk.Name);
                if (columnasPk.Contains(columnaFk.Name))
                    fkEnPk = true;
            }

            bool hijaUnica = tabla.Indexes.Cast<SmoIndex>().Any(i =>
                i.IsUnique &&
                string.Join("|", i.IndexedColumns.Cast<SmoIndexedColumn>().Select(ic => ic.Name)) ==
                string.Join("|", colsHija));

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

        if (columnasPk.Count == 2 && cantidadFks >= 2 && tablasReferenciadas.Count == 2)
            esquema.TablasUnionMuchosAMuchos.Add(tabla.Name);

        if (fkEnPk)
            esquema.EntidadesDebiles.Add(tabla.Name);
    }
}
