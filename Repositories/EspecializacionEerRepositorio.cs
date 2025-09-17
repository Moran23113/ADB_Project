using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Operaciones de bajo nivel para inspeccionar especializaciones EER directamente en SQL Server.
/// </summary>
public interface IEspecializacionEerRepositorio
{
    /// <summary>Obtiene los identificadores del supertipo que no aparecen en ningún subtipo.</summary>
    IEnumerable<int> ObtenerPadresSinHijo(string conexion, string entidadPadre, params string[] entidadesHija);
    /// <summary>Calcula cuántos registros aparecen en más de un subtipo (intersecciones).</summary>
    int ObtenerIntersecciones(string conexion, string entidadPadre, params string[] entidadesHija);
}

public class EspecializacionEerRepositorio : IEspecializacionEerRepositorio
{
    public IEnumerable<int> ObtenerPadresSinHijo(string conexion, string entidadPadre, params string[] entidadesHija)
    {
        // Esta consulta identifica participación total comparando la tabla padre con la unión de sus subtipos.
        var resultado = new List<int>();
        using var cn = new SqlConnection(conexion);
        cn.Open();

        var servidor = new Server(new ServerConnection(cn));
        var bd = servidor.Databases[cn.Database];
        var columnaPk = ObtenerColumnaPk(bd, entidadPadre);

        var selects = new List<string>();
        foreach (var hija in entidadesHija)
        {
            // Para cada subtipo se obtiene la columna FK que referencia al supertipo.
            var columnaFk = ObtenerColumnaFk(bd, hija, entidadPadre);
            selects.Add($"SELECT {columnaFk} AS IdPadre FROM {hija}");
        }

        var joinedChildren = string.Join(" UNION ALL ", selects);

        var sql = $@"
            SELECT p.{columnaPk}
            FROM {entidadPadre} p
            LEFT JOIN ({joinedChildren}) h ON p.{columnaPk} = h.IdPadre
            WHERE h.IdPadre IS NULL";

        // Ejecuta la consulta y recolecta los identificadores que no tienen correspondencia en los subtipos.
        using var cmd = new SqlCommand(sql, cn);
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
            resultado.Add(rd.GetInt32(0));

        return resultado;
    }

    public int ObtenerIntersecciones(string conexion, string entidadPadre, params string[] entidadesHija)
    {
        using var cn = new SqlConnection(conexion);
        cn.Open();
        var servidor = new Server(new ServerConnection(cn));
        var bd = servidor.Databases[cn.Database];
        int total = 0;

        for (int i = 0; i < entidadesHija.Length; i++)
        {
            for (int j = i + 1; j < entidadesHija.Length; j++)
            {
                // Se obtienen las columnas FK de ambos subtipos y se realiza un JOIN para contar registros comunes.
                var fk1 = ObtenerColumnaFk(bd, entidadesHija[i], entidadPadre);
                var fk2 = ObtenerColumnaFk(bd, entidadesHija[j], entidadPadre);

                var sql = $@"
                    SELECT COUNT(*)
                    FROM {entidadesHija[i]} a
                    JOIN {entidadesHija[j]} b
                      ON a.{fk1} = b.{fk2}";
                using var cmd = new SqlCommand(sql, cn);
                total += (int)cmd.ExecuteScalar()!;
            }
        }

        return total;
    }

    private static string ObtenerColumnaPk(Database bd, string tabla)
    {
        var t = bd.Tables[tabla];
        var pk = t.Indexes.Cast<Microsoft.SqlServer.Management.Smo.Index>()
                          .First(i => i.IndexKeyType == IndexKeyType.DriPrimaryKey);
        return pk.IndexedColumns[0].Name;
    }

    private static string ObtenerColumnaFk(Database bd, string tablaHija, string tablaPadre)
    {
        var t = bd.Tables[tablaHija];
        var fk = t.ForeignKeys.Cast<ForeignKey>()
                              .First(f => f.ReferencedTable == tablaPadre);
        return fk.Columns[0].Name;
    }
}
