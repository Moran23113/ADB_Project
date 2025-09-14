using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


public static class EERChoicesRestored
{
    
    
    
    public static string BuildCnnToRestoredDb(IConfiguration cfg, string nombreBD)
    {
        var csb = new SqlConnectionStringBuilder(cfg.GetConnectionString("SqlMaestra"));
        csb.InitialCatalog = nombreBD;
        return csb.ToString();
    }

    
    
    
    public static async Task EnsureTableAsync(string cnn)
    {
        const string sql = @"
IF OBJECT_ID('dbo.EER_UserChoices') IS NULL
BEGIN
  CREATE TABLE dbo.EER_UserChoices (
    Supertype   nvarchar(200)  NOT NULL,
    SubtypesCsv nvarchar(2000) NOT NULL,
    Disyuncion  nvarchar(20)   NOT NULL, 
    Totalidad   nvarchar(20)   NOT NULL, 
    CONSTRAINT PK_EER_UserChoices PRIMARY KEY (Supertype, SubtypesCsv)
  );
END";
        using var cn = new SqlConnection(cnn);
        await cn.OpenAsync();
        using var cmd = new SqlCommand(sql, cn);
        await cmd.ExecuteNonQueryAsync();
    }

    
    
    
    public static async Task SaveChoicesAsync(
        string cnn,
        Dictionary<string, string> disyuncion,
        Dictionary<string, string> totalidad,
        Dictionary<string, string> subtypesCsv)
    {
        await EnsureTableAsync(cnn);

        const string mergeSql = @"
MERGE dbo.EER_UserChoices AS t
USING (SELECT @sup AS Supertype, @subs AS SubtypesCsv) AS s
ON (t.Supertype = s.Supertype AND t.SubtypesCsv = s.SubtypesCsv)
WHEN MATCHED THEN UPDATE SET Disyuncion = @dis, Totalidad = @tot
WHEN NOT MATCHED THEN INSERT (Supertype, SubtypesCsv, Disyuncion, Totalidad)
VALUES (@sup, @subs, @dis, @tot);";

        using var cn = new SqlConnection(cnn);
        await cn.OpenAsync();

        foreach (var sup in disyuncion.Keys)
        {
            var dis = disyuncion[sup];
            var tot = totalidad.TryGetValue(sup, out var t) ? t : "Partial";
            var subs = subtypesCsv.TryGetValue(sup, out var s) ? s : "";

            using var cmd = new SqlCommand(mergeSql, cn);
            cmd.Parameters.AddWithValue("@sup", sup);
            cmd.Parameters.AddWithValue("@subs", subs);
            cmd.Parameters.AddWithValue("@dis", dis);
            cmd.Parameters.AddWithValue("@tot", tot);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    
    
    
    public static async Task<List<(string sup, string subs, string dis, string tot)>> LoadChoicesAsync(string cnn)
    {
        await EnsureTableAsync(cnn);

        using var cn = new SqlConnection(cnn);
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
