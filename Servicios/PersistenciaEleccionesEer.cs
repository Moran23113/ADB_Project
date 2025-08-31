using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Persistencia de elecciones EER (Disyunción / Totalidad) dentro de la BD restaurada.
/// Crea y usa la tabla interna dbo.EER_UserChoices (clave compuesta: Supertype + SubtypesCsv).
/// </summary>
public static class PersistenciaEleccionesEer
{
    /// <summary>
    /// Construye la cadena a la BD restaurada reutilizando servidor/credenciales de "SqlMaestra".
    /// </summary>
    public static string ConstruirConexionBdRestaurada(IConfiguration cfg, string nombreBd)
    {
        var csb = new SqlConnectionStringBuilder(cfg.GetConnectionString("SqlMaestra"))
        {
            InitialCatalog = nombreBd
        };
        return csb.ToString();
    }

    /// <summary>
    /// Asegura la existencia de dbo.EER_UserChoices en la BD (idempotente).
    /// </summary>
    public static async Task AsegurarTablaAsync(string conexion)
    {
        const string sql = @"
IF OBJECT_ID('dbo.EER_UserChoices') IS NULL
BEGIN
  CREATE TABLE dbo.EER_UserChoices (
    Supertype   nvarchar(200)  NOT NULL,
    SubtypesCsv nvarchar(2000) NOT NULL,
    Disyuncion  nvarchar(20)   NOT NULL, -- 'Exclusive' | 'Overlapping'
    Totalidad   nvarchar(20)   NOT NULL, -- 'Total' | 'Partial'
    CONSTRAINT PK_EER_UserChoices PRIMARY KEY (Supertype, SubtypesCsv)
  );
END";
        using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Guarda (MERGE) las elecciones por Supertype/SubtypesCsv.
    /// </summary>
    public static async Task GuardarEleccionesAsync(
        string conexion,
        Dictionary<string, string> disyuncion,
        Dictionary<string, string> totalidad,
        Dictionary<string, string> subtiposCsv)
    {
        await AsegurarTablaAsync(conexion);

        const string mergeSql = @"
MERGE dbo.EER_UserChoices AS t
USING (SELECT @sup AS Supertype, @subs AS SubtypesCsv) AS s
ON (t.Supertype = s.Supertype AND t.SubtypesCsv = s.SubtypesCsv)
WHEN MATCHED THEN UPDATE SET Disyuncion = @dis, Totalidad = @tot
WHEN NOT MATCHED THEN INSERT (Supertype, SubtypesCsv, Disyuncion, Totalidad)
VALUES (@sup, @subs, @dis, @tot);";

        using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();

        foreach (var sup in disyuncion.Keys)
        {
            var dis = disyuncion[sup];
            var tot = totalidad.TryGetValue(sup, out var t) ? t : "Partial";
            var subs = subtiposCsv.TryGetValue(sup, out var s) ? s : "";

            using var cmd = new SqlCommand(mergeSql, cn);
            cmd.Parameters.AddWithValue("@sup", sup);
            cmd.Parameters.AddWithValue("@subs", subs);
            cmd.Parameters.AddWithValue("@dis", dis);
            cmd.Parameters.AddWithValue("@tot", tot);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Carga todas las elecciones almacenadas.
    /// </summary>
    public static async Task<List<(string sup, string subs, string dis, string tot)>> CargarEleccionesAsync(string conexion)
    {
        await AsegurarTablaAsync(conexion);

        using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT Supertype, SubtypesCsv, Disyuncion, Totalidad FROM dbo.EER_UserChoices", cn);

        var list = new List<(string, string, string, string)>();
        using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            list.Add((rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetString(3)));
        return list;
    }
}
