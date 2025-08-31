using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ABD_Project.Modelos;

/// <summary>
/// Guarda y aplica las elecciones del usuario para jerarquías EER
/// dentro de la base de datos restaurada.
/// </summary>
public class EERChoicesService
{
    private readonly IConfiguration _cfg;
    public EERChoicesService(IConfiguration cfg) => _cfg = cfg;

    /// <summary>
    /// Construye la cadena a la BD restaurada reutilizando servidor/credenciales de "SqlMaestra".
    /// </summary>
    public string ConstruirConexionBdRestaurada(string nombreBd)
    {
        var csb = new SqlConnectionStringBuilder(_cfg.GetConnectionString("SqlMaestra"))
        {
            InitialCatalog = nombreBd
        };
        return csb.ToString();
    }

    private static async Task AsegurarTablaAsync(string conexion)
    {
        const string sql = @"\
IF OBJECT_ID('dbo.EER_UserChoices') IS NULL\
BEGIN\
  CREATE TABLE dbo.EER_UserChoices (\
    Supertype   nvarchar(200)  NOT NULL,\
    SubtypesCsv nvarchar(2000) NOT NULL,\
    Disyuncion  nvarchar(20)   NOT NULL,\
    Totalidad   nvarchar(20)   NOT NULL,\
    CONSTRAINT PK_EER_UserChoices PRIMARY KEY (Supertype, SubtypesCsv)\
  );\
END";
        using var cn = new SqlConnection(conexion);
        await cn.OpenAsync();
        using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Guarda (MERGE) las elecciones por Supertype/SubtypesCsv.
    /// </summary>
    public async Task GuardarEleccionesAsync(
        string conexion,
        Dictionary<string, string> disyuncion,
        Dictionary<string, string> totalidad,
        Dictionary<string, string> subtiposCsv)
    {
        await AsegurarTablaAsync(conexion);

        const string mergeSql = @"\
MERGE dbo.EER_UserChoices AS t\
USING (SELECT @sup AS Supertype, @subs AS SubtypesCsv) AS s\
ON (t.Supertype = s.Supertype AND t.SubtypesCsv = s.SubtypesCsv)\
WHEN MATCHED THEN UPDATE SET Disyuncion = @dis, Totalidad = @tot\
WHEN NOT MATCHED THEN INSERT (Supertype, SubtypesCsv, Disyuncion, Totalidad)\
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

    /// <summary>Carga todas las elecciones almacenadas.</summary>
    public async Task<List<(string sup, string subs, string dis, string tot)>> CargarEleccionesAsync(string conexion)
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

    /// <summary>
    /// Aplica las elecciones sobre las jerarquías detectadas.
    /// </summary>
    public async Task AplicarEleccionesAsync(string conexion, List<JerarquiaEer> jerarquias)
    {
        var choices = await CargarEleccionesAsync(conexion);

        foreach (var j in jerarquias)
        {
            var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var c = choices.FirstOrDefault(x =>
                x.sup.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(c.sup))
            {
                j.Disyuncion = c.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                    ? EerDisyuncion.Exclusiva : EerDisyuncion.Solapada;
                j.Totalidad = c.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                    ? EerTotalidad.Total : EerTotalidad.Parcial;
                j.Evidencia = "Elección del usuario aplicada.";
            }
        }
    }
}
