using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;

/// <summary>
/// Servicio para restaurar y eliminar bases de datos SQL Server a partir de un archivo .bak.
/// - Usa una cadena de conexión "maestra" (por ejemplo, a master) para ejecutar RESTORE/DROP.
/// - Genera un nombre aleatorio para la BD restaurada con un prefijo configurable.
/// </summary>
public class ServicioRestauracionSql
{
    private readonly string _cnnMaestra;
    private readonly ILogger<ServicioRestauracionSql> _logger;

    /// <summary>
    /// Crea el servicio leyendo la cadena de conexión "SqlMaestra" (appsettings.json / secretos).
    /// </summary>
    public ServicioRestauracionSql(IConfiguration cfg, ILogger<ServicioRestauracionSql> logger)
    {
        _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;
        _logger = logger;
    }

    /// <summary>
    /// Restaura una base de datos desde un archivo .bak en el servidor SQL.
    /// - Detecta las rutas por defecto del instance para data/log.
    /// - Lee los nombres lógicos del backup (RESTORE FILELISTONLY).
    /// - RESTORE DATABASE con MOVE a archivos físicos nuevos (.mdf/.ldf).
    /// </summary>
    /// <param name="rutaBak">Ruta absoluta del archivo .bak en el servidor (accesible por el servicio SQL Server).</param>
    /// <param name="prefijoNombre">Prefijo para el nombre de la BD restaurada (por defecto "ER").</param>
    /// <returns>Nombre de la base de datos restaurada.</returns>
    /// <exception cref="Exception">Si el .bak no contiene archivos o es incompatible.</exception>
    public async Task<string> RestaurarAsync(string rutaBak, string prefijoNombre = "ER")
    {
        var nombreBD = GenerarNombre(prefijoNombre);
        using var cn = new SqlConnection(_cnnMaestra);
        await cn.OpenAsync();

        var (rutaDatos, rutaLog) = await ObtenerRutasPorDefectoAsync(cn);
        var archivos = await LeerArchivosLogicosAsync(cn, rutaBak);
        var (logicoDatos, logicoLog) = SeleccionarArchivosLogicos(archivos);
        var (fisicoDatos, fisicoLog) = ConstruirRutasFisicas(rutaDatos, rutaLog, nombreBD);
        await RestaurarBaseAsync(cn, rutaBak, nombreBD, logicoDatos, logicoLog, fisicoDatos, fisicoLog);

        return nombreBD;
    }

    private string GenerarNombre(string prefijo) => $"{prefijo}_{Guid.NewGuid():N}".ToUpper();

    private static async Task<(string datos, string log)> ObtenerRutasPorDefectoAsync(SqlConnection cn)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
       CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(4000));";
        using var rd = await cmd.ExecuteReaderAsync();
        rd.Read();
        return (rd.GetString(0), rd.GetString(1));
    }

    private static async Task<List<(string Logico, string Tipo)>> LeerArchivosLogicosAsync(SqlConnection cn, string rutaBak)
    {
        var archivos = new List<(string Logico, string Tipo)>();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = @p";
        cmd.Parameters.Add(new SqlParameter("@p", SqlDbType.NVarChar, 4000) { Value = rutaBak });
        using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            archivos.Add((rd["LogicalName"].ToString()!, rd["Type"].ToString()!));
        if (archivos.Count == 0)
            throw new Exception("El archivo .bak no contiene archivos o es incompatible.");
        return archivos;
    }

    private static (string datos, string log) SeleccionarArchivosLogicos(List<(string Logico, string Tipo)> archivos)
    {
        var logicoDatos = archivos.First(a => a.Tipo == "D").Logico;
        var logicoLog = archivos.First(a => a.Tipo == "L").Logico;
        return (logicoDatos, logicoLog);
    }

    private static (string datos, string log) ConstruirRutasFisicas(string rutaDatos, string rutaLog, string nombreBD)
    {
        var fisicoDatos = Path.Combine(rutaDatos, $"{nombreBD}.mdf");
        var fisicoLog = Path.Combine(rutaLog, $"{nombreBD}_log.ldf");
        return (fisicoDatos, fisicoLog);
    }

    private async Task RestaurarBaseAsync(
        SqlConnection cn,
        string rutaBak,
        string nombreBD,
        string logicoDatos,
        string logicoLog,
        string fisicoDatos,
        string fisicoLog)
    {
        using var cmd = cn.CreateCommand();
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

        _logger.LogInformation("Restaurando base de datos {Nombre}", nombreBD);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Elimina (DROP) una base de datos del servidor, forzando SINGLE_USER y rollback inmediato.
    /// </summary>
    /// <param name="nombreBD">Nombre de la BD a eliminar.</param>
    public async Task EliminarBaseDatosAsync(string nombreBD)
    {
        using var cn = new SqlConnection(_cnnMaestra);
        await cn.OpenAsync();

        using var cmd = cn.CreateCommand();
        // Ojo: no se puede parametrizar un identificador (nombre de BD) con @param,
        // por eso se incrusta. Se recomienda validar el formato o usar QUOTENAME.
        cmd.CommandText = @"
IF DB_ID(@db) IS NOT NULL
BEGIN
  -- Forzar desconexión de sesiones abiertas
  ALTER DATABASE " + "[" + nombreBD + @"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE " + "[" + nombreBD + @"]" + @";
END";
        cmd.Parameters.AddWithValue("@db", nombreBD);

        _logger.LogInformation("Eliminando base de datos {Nombre}", nombreBD);
        await cmd.ExecuteNonQueryAsync();
    }
}
