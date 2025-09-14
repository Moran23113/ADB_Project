using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

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
            var servidor = new Server(new ServerConnection(conexion));

            string rutaDatos = servidor.DefaultFile;
            string rutaLogs = servidor.DefaultLog;

            var restore = new Restore
            {
                Database = nombreBd,
                Action = RestoreActionType.Database,
                ReplaceDatabase = true
            };
            restore.Devices.AddDevice(rutaBackup, DeviceType.File);

            var fileList = restore.ReadFileList(servidor);
            string logicoDatos = fileList.Rows[0]["LogicalName"].ToString()!;
            string logicoLog = fileList.Rows[1]["LogicalName"].ToString()!;

            string rutaMdf = Path.Combine(rutaDatos, $"{nombreBd}.mdf");
            string rutaLdf = Path.Combine(rutaLogs, $"{nombreBd}_log.ldf");

            restore.RelocateFiles.Add(new RelocateFile(logicoDatos, rutaMdf));
            restore.RelocateFiles.Add(new RelocateFile(logicoLog, rutaLdf));

            await Task.Run(() => restore.SqlRestore(servidor));
        }
        catch (SmoException ex)
        {
            throw new InvalidOperationException("Error al restaurar la base de datos.", ex);
        }

        return nombreBd;
    }

    public async Task EliminarBaseDatosAsync(string nombreBd)
    {
        using var conexion = new SqlConnection(cadenaMaestra);
        await conexion.OpenAsync();
        var servidor = new Server(new ServerConnection(conexion));
        await Task.Run(() => servidor.KillDatabase(nombreBd));
    }
}
