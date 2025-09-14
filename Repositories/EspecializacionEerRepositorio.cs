using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;

public interface IEspecializacionEerRepositorio
{
    IEnumerable<int> ObtenerPadresSinHijo(string conexion, string entidadPadre, params string[] entidadesHija);
    int ObtenerIntersecciones(string conexion, string entidadPadre, params string[] entidadesHija);
}

public class EspecializacionEerRepositorio : IEspecializacionEerRepositorio
{
    public IEnumerable<int> ObtenerPadresSinHijo(string conexion, string entidadPadre, params string[] entidadesHija)
    {
        var resultado = new List<int>();
        using var cn = new SqlConnection(conexion);
        cn.Open();

        var columnaPk = ObtenerColumnaPk(cn, entidadPadre);

        var selects = new List<string>();
        foreach (var hija in entidadesHija)
        {
            var columnaFk = ObtenerColumnaFk(cn, hija, entidadPadre);
            selects.Add($"SELECT {columnaFk} AS IdPadre FROM {hija}");
        }

        var joinedChildren = string.Join(" UNION ALL ", selects);

        var sql = $@"
            SELECT p.{columnaPk}
            FROM {entidadPadre} p
            LEFT JOIN ({joinedChildren}) h ON p.{columnaPk} = h.IdPadre
            WHERE h.IdPadre IS NULL";

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
        int total = 0;

        for (int i = 0; i < entidadesHija.Length; i++)
        {
            for (int j = i + 1; j < entidadesHija.Length; j++)
            {
                var fk1 = ObtenerColumnaFk(cn, entidadesHija[i], entidadPadre);
                var fk2 = ObtenerColumnaFk(cn, entidadesHija[j], entidadPadre);

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

    private static string ObtenerColumnaPk(SqlConnection cn, string tabla)
    {
        var sql = @"
            SELECT c.name
            FROM sys.key_constraints kc
            JOIN sys.index_columns ic
              ON kc.parent_object_id = ic.object_id
             AND kc.unique_index_id = ic.index_id
            JOIN sys.columns c
              ON ic.object_id = c.object_id
             AND ic.column_id = c.column_id
            WHERE kc.type = 'PK' AND OBJECT_NAME(kc.parent_object_id) = @tabla";

        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@tabla", tabla);
        return (string)cmd.ExecuteScalar()!;
    }

    private static string ObtenerColumnaFk(SqlConnection cn, string tablaHija, string tablaPadre)
    {
        var sql = @"
            SELECT cc.name
            FROM sys.foreign_key_columns fkc
            JOIN sys.tables tp ON fkc.referenced_object_id = tp.object_id
            JOIN sys.tables tc ON fkc.parent_object_id = tc.object_id
            JOIN sys.columns cc
              ON fkc.parent_object_id = cc.object_id
             AND fkc.parent_column_id = cc.column_id
            WHERE tp.name = @padre AND tc.name = @hija";

        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@padre", tablaPadre);
        cmd.Parameters.AddWithValue("@hija", tablaHija);
        return (string)cmd.ExecuteScalar()!;
    }
}

