using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

/// <summary>
/// Define las operaciones relacionadas con la restauración y eliminación
/// temporal de bases de datos a partir de archivos <c>.bak</c>.
/// </summary>
public interface IRestauracionRepositorio
{
    /// <summary>
    /// Restaura un archivo de respaldo en el servidor SQL y devuelve el nombre de la base creada.
    /// </summary>
    /// <param name="rutaRespaldo">Ruta física del archivo <c>.bak</c> que se desea restaurar.</param>
    /// <param name="prefijo">Prefijo que se utiliza para nombrar la base temporal restaurada.</param>
    Task<string> RestaurarAsync(string rutaRespaldo, string prefijo = "ER");

    /// <summary>
    /// Elimina la base de datos restaurada del servidor SQL para liberar recursos.
    /// </summary>
    /// <param name="nombreBd">Nombre de la base de datos que se desea eliminar.</param>
    Task EliminarBaseDatosAsync(string nombreBd);
}

public class RestauracionRepositorio : IRestauracionRepositorio
{
    private readonly string _cadenaConexionMaestra;

    public RestauracionRepositorio(IConfiguration configuracion)
    {
        _cadenaConexionMaestra = configuracion.GetConnectionString("SqlMaestra")!;
    }

    /// <summary>
    /// Ejecuta paso a paso la restauración de un archivo <c>.bak</c> en SQL Server.
    /// </summary>
    /// <remarks>
    /// Cada fragmento del método está documentado para clarificar cómo se construye el nombre
    /// de la base temporal, cómo se configuran las rutas de datos y logs y cómo se ejecuta el
    /// comando de restauración mediante SMO (SQL Server Management Objects).
    /// </remarks>
    public async Task<string> RestaurarAsync(string rutaRespaldo, string prefijo = "ER")
    {
        // Se genera un nombre único utilizando el prefijo indicado. Las bases restauradas
        // se nombran así para que sean fáciles de identificar y evitar colisiones.
        string nombreBd = $"{prefijo}_{Guid.NewGuid():N}".ToUpper();

        try
        {
            // Abre una conexión a la base maestra del servidor donde se restaurará el respaldo.
            using var conexion = new SqlConnection(_cadenaConexionMaestra);
            await conexion.OpenAsync();

            // SMO requiere envolver la conexión en un ServerConnection para poder invocar
            // operaciones administrativas como la restauración.
            var servidor = new Server(new ServerConnection(conexion));

            // Recupera las rutas por defecto configuradas en SQL Server para los archivos MDF y LDF.
            string rutaDatos = servidor.DefaultFile;
            string rutaLogs = servidor.DefaultLog;

            // Configura el objeto Restore indicando que se trata de una restauración completa
            // de base de datos y que se debe reemplazar cualquier base existente con el mismo nombre.
            var restauracion = new Restore
            {
                Database = nombreBd,
                Action = RestoreActionType.Database,
                ReplaceDatabase = true
            };
            // El archivo .bak se agrega como dispositivo de entrada de la restauración.
            restauracion.Devices.AddDevice(rutaRespaldo, DeviceType.File);

            // ReadFileList obtiene los nombres lógicos originales de los archivos de datos y log
            // almacenados en el respaldo para poder reubicarlos en la instancia actual.
            var listaArchivos = restauracion.ReadFileList(servidor);
            string nombreLogicoDatos = listaArchivos.Rows[0]["LogicalName"].ToString()!;
            string nombreLogicoLog = listaArchivos.Rows[1]["LogicalName"].ToString()!;

            // Define la nueva ruta física para los archivos restaurados dentro del servidor.
            string rutaMdf = Path.Combine(rutaDatos, $"{nombreBd}.mdf");
            string rutaLdf = Path.Combine(rutaLogs, $"{nombreBd}_log.ldf");

            // RelocateFiles indica a SMO que coloque los archivos físicos en las rutas calculadas
            // en lugar de las rutas originales almacenadas en el respaldo.
            restauracion.RelocateFiles.Add(new RelocateFile(nombreLogicoDatos, rutaMdf));
            restauracion.RelocateFiles.Add(new RelocateFile(nombreLogicoLog, rutaLdf));

            // Ejecuta la restauración. Se usa Task.Run porque SMO sólo ofrece API sincrónica.
            await Task.Run(() => restauracion.SqlRestore(servidor));
        }
        catch (SmoException ex)
        {
            // Cualquier error de SMO se envuelve en InvalidOperationException para que el
            // controlador pueda mostrar un mensaje amigable al usuario.
            throw new InvalidOperationException("Error al restaurar la base de datos.", ex);
        }

        return nombreBd;
    }

    /// <summary>
    /// Elimina una base de datos temporal previamente restaurada.
    /// </summary>
    public async Task EliminarBaseDatosAsync(string nombreBd)
    {
        // Abre una conexión a la base maestra y crea un objeto Server de SMO.
        using var conexion = new SqlConnection(_cadenaConexionMaestra);
        await conexion.OpenAsync();
        var servidor = new Server(new ServerConnection(conexion));

        // KillDatabase fuerza la eliminación incluso si hay conexiones pendientes.
        await Task.Run(() => servidor.KillDatabase(nombreBd));
    }
}
