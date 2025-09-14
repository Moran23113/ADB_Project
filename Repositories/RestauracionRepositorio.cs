using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

public interface IRestauracionRepositorio
{
    Task<string> RestaurarAsync(string rutaRespaldo, string prefijo = "ER");
    Task EliminarBaseDatosAsync(string nombreBd);
}

public class RestauracionRepositorio : IRestauracionRepositorio
{
    private readonly string _cadenaConexionMaestra;

    public RestauracionRepositorio(IConfiguration configuracion)
    {
        _cadenaConexionMaestra = configuracion.GetConnectionString("SqlMaestra")!;
    }

    public async Task<string> RestaurarAsync(string rutaRespaldo, string prefijo = "ER")
    {
        string nombreBd = $"{prefijo}_{Guid.NewGuid():N}".ToUpper();

        try
        {
            using var conexion = new SqlConnection(_cadenaConexionMaestra);
            await conexion.OpenAsync();
            var servidor = new Server(new ServerConnection(conexion));

            string rutaDatos = servidor.DefaultFile;
            string rutaLogs = servidor.DefaultLog;

            var restauracion = new Restore
            {
                Database = nombreBd,
                Action = RestoreActionType.Database,
                ReplaceDatabase = true
            };
            restauracion.Devices.AddDevice(rutaRespaldo, DeviceType.File);

            var listaArchivos = restauracion.ReadFileList(servidor);
            string nombreLogicoDatos = listaArchivos.Rows[0]["LogicalName"].ToString()!;
            string nombreLogicoLog = listaArchivos.Rows[1]["LogicalName"].ToString()!;

            string rutaMdf = Path.Combine(rutaDatos, $"{nombreBd}.mdf");
            string rutaLdf = Path.Combine(rutaLogs, $"{nombreBd}_log.ldf");

            restauracion.RelocateFiles.Add(new RelocateFile(nombreLogicoDatos, rutaMdf));
            restauracion.RelocateFiles.Add(new RelocateFile(nombreLogicoLog, rutaLdf));

            await Task.Run(() => restauracion.SqlRestore(servidor));
        }
        catch (SmoException ex)
        {
            throw new InvalidOperationException("Error al restaurar la base de datos.", ex);
        }

        return nombreBd;
    }

    public async Task EliminarBaseDatosAsync(string nombreBd)
    {
        using var conexion = new SqlConnection(_cadenaConexionMaestra);
        await conexion.OpenAsync();
        var servidor = new Server(new ServerConnection(conexion));
        await Task.Run(() => servidor.KillDatabase(nombreBd));
    }
}
