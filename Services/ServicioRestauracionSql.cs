using Microsoft.Data.SqlClient;
using System.Data;

public class ServicioRestauracionSql
{
    private readonly string _cnnMaestra;

    public ServicioRestauracionSql(IConfiguration cfg)
    {
        _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;
    }

    public async Task<string> RestaurarAsync(string rutaBak, string prefijoNombre = "ER")
    {
        var nombreBD = $"{prefijoNombre}_{Guid.NewGuid():N}".ToUpper();

        using var cn = new SqlConnection(_cnnMaestra);
        await cn.OpenAsync();

        // Rutas por defecto del instance
        string rutaDatos, rutaLog;
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
       CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(4000));";
            using var rd = await cmd.ExecuteReaderAsync();
            rd.Read();
            rutaDatos = rd.GetString(0);
            rutaLog = rd.GetString(1);
        }

        // Nombres lógicos del .bak
        var archivos = new List<(string Logico, string Tipo)>();
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = @p";
            cmd.Parameters.Add(new SqlParameter("@p", SqlDbType.NVarChar, 4000) { Value = rutaBak });
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                archivos.Add((rd["LogicalName"].ToString()!, rd["Type"].ToString()!));
        }
        if (archivos.Count == 0) throw new Exception("El archivo .bak no contiene archivos o es incompatible.");

        var logicoDatos = archivos.First(a => a.Tipo == "D").Logico;
        var logicoLog = archivos.First(a => a.Tipo == "L").Logico;

        var fisicoDatos = Path.Combine(rutaDatos, $"{nombreBD}.mdf");
        var fisicoLog = Path.Combine(rutaLog, $"{nombreBD}_log.ldf");

        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandTimeout = 0;
            cmd.CommandText = @"
RESTORE DATABASE [" + nombreBD + @"] FROM DISK = @p
WITH MOVE @ld TO @d,
     MOVE @ll TO @l,
     REPLACE, RECOVERY, STATS = 5;";
            cmd.Parameters.AddWithValue("@p", rutaBak);
            cmd.Parameters.AddWithValue("@ld", logicoDatos);
            cmd.Parameters.AddWithValue("@ll", logicoLog);
            cmd.Parameters.AddWithValue("@d", fisicoDatos);
            cmd.Parameters.AddWithValue("@l", fisicoLog);
            await cmd.ExecuteNonQueryAsync();
        }

        return nombreBD;
    }

    public async Task EliminarBaseDatosAsync(string nombreBD)
    {
        using var cn = new SqlConnection(_cnnMaestra);
        await cn.OpenAsync();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
IF DB_ID(@db) IS NOT NULL
BEGIN
  ALTER DATABASE [" + nombreBD + @"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [" + nombreBD + @"];
END";
        cmd.Parameters.AddWithValue("@db", nombreBD);
        await cmd.ExecuteNonQueryAsync();
    }
}
