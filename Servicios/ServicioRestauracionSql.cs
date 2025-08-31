using Microsoft.Data.SqlClient;
using System.Data;

namespace ABD_Project.Servicios;

/// <summary>
/// Servicio para restaurar y eliminar bases de datos SQL Server a partir de un archivo .bak.
/// - Usa una cadena de conexión "maestra" (por ejemplo, a master) para ejecutar RESTORE/DROP.
/// - Genera un nombre aleatorio para la BD restaurada con un prefijo configurable.
/// </summary>
public class ServicioRestauracionSql
{
    private readonly string _cnnMaestra;

    /// <summary>
    /// Crea el servicio leyendo la cadena de conexión "SqlMaestra" (appsettings.json / secretos).
    /// </summary>
    public ServicioRestauracionSql(IConfiguration cfg)
    {
        _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;
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
        // Genera un nombre único y “amigable” (sin guiones, mayúsculas).
        var nombreBD = $"{prefijoNombre}_{Guid.NewGuid():N}".ToUpper();

        using var cn = new SqlConnection(_cnnMaestra);
        await cn.OpenAsync();

        // 1) Obtener rutas por defecto (data y log) del instance
        string rutaDatos, rutaLog;
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
       CAST(SERVERPROPERTY('InstanceDefaultLogPath')  AS nvarchar(4000));";
            using var rd = await cmd.ExecuteReaderAsync();
            rd.Read(); // serverproperty devuelve una fila
            rutaDatos = rd.GetString(0);
            rutaLog = rd.GetString(1);
        }

        // 2) Leer nombres lógicos de los archivos dentro del .bak
        var archivos = new List<(string Logico, string Tipo)>();
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "RESTORE FILELISTONLY FROM DISK = @p";
            cmd.Parameters.Add(new SqlParameter("@p", SqlDbType.NVarChar, 4000) { Value = rutaBak });
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                // Type: 'D' = data, 'L' = log (puede haber múltiples).
                archivos.Add((rd["LogicalName"].ToString()!, rd["Type"].ToString()!));
            }
        }
        if (archivos.Count == 0)
            throw new Exception("El archivo .bak no contiene archivos o es incompatible.");

        // Seleccionar un archivo lógico de datos y uno de log (asumiendo estructura típica 1D/1L)
        var logicoDatos = archivos.First(a => a.Tipo == "D").Logico;
        var logicoLog = archivos.First(a => a.Tipo == "L").Logico;

        // 3) Construir rutas físicas destino (.mdf/.ldf) en las carpetas por defecto del instance
        var fisicoDatos = Path.Combine(rutaDatos, $"{nombreBD}.mdf");
        var fisicoLog = Path.Combine(rutaLog, $"{nombreBD}_log.ldf");

        // 4) Restaurar la base con MOVE + REPLACE + RECOVERY
        using (var cmd = cn.CreateCommand())
        {
            // Para restores grandes, sin límite de tiempo
            cmd.CommandTimeout = 0;

            // Nota: Se parametriza el DISK y los nombres lógicos/físicos.
            // Los identificadores (nombre de BD) se insertan en el T-SQL (no parametrizable como identificador),
            // pero el nombre lo generamos nosotros, no proviene del usuario.
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

        await cmd.ExecuteNonQueryAsync();
    }
}
