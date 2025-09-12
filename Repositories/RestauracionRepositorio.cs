using Microsoft.Data.SqlClient;
using System.Data;

public interface IRestauracionRepositorio
{
    Task<string> RestaurarAsync(string rutaBackup, string prefijo = "ER");
    Task EliminarBaseDatosAsync(string nombreBd);
}

public class RestauracionRepositorio : IRestauracionRepositorio
{
    private readonly string cadenaMaestra;

    public RestauracionRepositorio(IConfiguration configuracion)
    {
        cadenaMaestra = configuracion.GetConnectionString("SqlMaestra")!;
    }

    public async Task<string> RestaurarAsync(string rutaBackup, string prefijo = "ER")
    {
        string nombreBd = $"{prefijo}_{Guid.NewGuid():N}".ToUpper();

        try
        {
            using var conexion = new SqlConnection(cadenaMaestra);
            await conexion.OpenAsync();

            var rutas = await ObtenerRutasPorDefectoAsync(conexion);
            var nombresLogicos = await ObtenerNombresLogicosAsync(conexion, rutaBackup);

            string rutaMdf = Path.Combine(rutas.rutaDatos, $"{nombreBd}.mdf");
            string rutaLdf = Path.Combine(rutas.rutaLogs, $"{nombreBd}_log.ldf");

            await EjecutarRestoreAsync(
                conexion, nombreBd, rutaBackup,
                nombresLogicos.logicoDatos, nombresLogicos.logicoLog,
                rutaMdf, rutaLdf);
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException("Error al restaurar la base de datos.", ex);
        }

        return nombreBd;
    }

    private static async Task<(string rutaDatos, string rutaLogs)> ObtenerRutasPorDefectoAsync(SqlConnection conexion)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = @"SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
                                       CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(4000));";
        using var lector = await comando.ExecuteReaderAsync();
        lector.Read();
        return (lector.GetString(0), lector.GetString(1));
    }

    private static async Task<(string logicoDatos, string logicoLog)> ObtenerNombresLogicosAsync(SqlConnection conexion, string rutaBackup)
    {
        var archivos = new List<(string nombre, string tipo)>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "RESTORE FILELISTONLY FROM DISK = @ruta";
        comando.Parameters.Add(new SqlParameter("@ruta", SqlDbType.NVarChar, 4000) { Value = rutaBackup });
        using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            archivos.Add((lector["LogicalName"].ToString()!, lector["Type"].ToString()!));

        string archivoDatos = archivos.First(a => a.tipo == "D").nombre;
        string archivoLog = archivos.First(a => a.tipo == "L").nombre;
        return (archivoDatos, archivoLog);
    }

    private static async Task EjecutarRestoreAsync(
        SqlConnection conexion, string nombreBd, string rutaBackup,
        string logicoDatos, string logicoLog,
        string rutaMdf, string rutaLdf)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandTimeout = 0;
        comando.CommandText = $@"RESTORE DATABASE [{nombreBd}] FROM DISK = @ruta
WITH MOVE @ld TO @mdf,
     MOVE @ll TO @ldf,
     REPLACE, RECOVERY, STATS = 5;";
        comando.Parameters.AddWithValue("@ruta", rutaBackup);
        comando.Parameters.AddWithValue("@ld", logicoDatos);
        comando.Parameters.AddWithValue("@ll", logicoLog);
        comando.Parameters.AddWithValue("@mdf", rutaMdf);
        comando.Parameters.AddWithValue("@ldf", rutaLdf);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task EliminarBaseDatosAsync(string nombreBd)
    {
        using var conexion = new SqlConnection(cadenaMaestra);
        await conexion.OpenAsync();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $@"IF DB_ID(@nombre) IS NOT NULL
BEGIN
  ALTER DATABASE [{nombreBd}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE [{nombreBd}];
END";
        comando.Parameters.AddWithValue("@nombre", nombreBd);
        await comando.ExecuteNonQueryAsync();
    }
}
