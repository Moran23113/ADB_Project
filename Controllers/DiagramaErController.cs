using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controlador encargado de la carga de archivos .bak, restauración temporal de la BD,
/// lectura de su esquema y construcción de un diagrama entidad–relación en notación Chen.
/// </summary>
public class DiagramaErController : Controller
{
    // Dependencias inyectadas a través del constructor
    private readonly IWebHostEnvironment _entorno;      // Entorno de hosting (rutas físicas de la app)
    private readonly IConfiguration _cfg;              // Configuración (para leer claves de appsettings.json, etc.)
    private readonly ServicioRestauracionSql _restaurador; // Servicio para restaurar/eliminar bases de datos desde .bak
    private readonly LectorEsquemaSql _lector;         // Servicio que lee el esquema de la BD (tablas, claves, relaciones)
    private readonly ConstructorDiagramaChen _constructor; // Servicio que convierte el esquema en un diagrama Mermaid/Chen

    /// <summary>
    /// Constructor: inicializa el controlador con las dependencias requeridas.
    /// </summary>
    public DiagramaErController(
        IWebHostEnvironment entorno,
        IConfiguration cfg,
        ServicioRestauracionSql restaurador,
        LectorEsquemaSql lector,
        ConstructorDiagramaChen constructor)
    {
        _entorno = entorno;
        _cfg = cfg;
        _restaurador = restaurador;
        _lector = lector;
        _constructor = constructor;
    }

    /// <summary>
    /// Vista GET para mostrar el formulario de carga del archivo .bak.
    /// </summary>
    [HttpGet]
    public IActionResult Subir() => View();

    /// <summary>
    /// Acción POST que recibe un archivo .bak, lo guarda temporalmente,
    /// restaura la base de datos en el servidor SQL, lee su esquema
    /// y genera un diagrama ER en notación Chen usando Mermaid.
    /// </summary>
    /// <param name="archivoBak">Archivo .bak subido por el usuario.</param>
    /// <returns>Vista con el resultado del diagrama o error si falla.</returns>
    [HttpPost]
    [RequestSizeLimit(1_000_000_000)] // Límite de 1 GB para permitir backups grandes
    public async Task<IActionResult> Subir(IFormFile archivoBak)
    {
        // Validación: que se haya enviado un archivo
        if (archivoBak == null || archivoBak.Length == 0)
        {
            ModelState.AddModelError("", "Sube un archivo .bak");
            return View();
        }

        // Carpeta de subida configurada en appsettings.json ("Upload:Carpeta")
        // o por defecto "App_Data/Uploads"
        var carpeta = _cfg["Upload:Carpeta"] ?? "App_Data/Uploads";

        // Ruta física donde se guardará temporalmente el .bak
        var raiz = Path.Combine(_entorno.ContentRootPath, carpeta);
        Directory.CreateDirectory(raiz);
        var rutaBak = Path.Combine(raiz, $"{Guid.NewGuid():N}.bak");

        // Guardar el archivo en disco
        using (var fs = System.IO.File.Create(rutaBak))
            await archivoBak.CopyToAsync(fs);

        string nombreBD = "";
        try
        {
            // 1. Restaurar la BD temporalmente en el servidor SQL
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");

            // 2. Leer esquema de tablas, PKs, FKs, etc.
            var snap = await _lector.LeerAsync(nombreBD);

            // 3. Construir diagrama en formato Mermaid (Chen)
            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _constructor.Construir(snap);

            // Mostrar la vista "Resultado" con el diagrama generado
            return View("Resultado");
        }
        catch (Exception ex)
        {
            // Captura errores (restauración, lectura, construcción)
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
        finally
        {
            // Elimina el archivo temporal .bak del disco
            try { System.IO.File.Delete(rutaBak); } catch { }
        }
    }

    /// <summary>
    /// Acción POST que elimina la base de datos restaurada temporalmente.
    /// Se usa cuando ya no se requiere el diagrama o se quiere liberar espacio.
    /// </summary>
    /// <param name="nombreBD">Nombre de la base de datos a eliminar.</param>
    /// <returns>Redirige a la vista de subida con mensaje de éxito o error.</returns>
    [HttpPost]
    public async Task<IActionResult> EliminarBase(string nombreBD)
    {
        try
        {
            await _restaurador.EliminarBaseDatosAsync(nombreBD);
            TempData["msg"] = $"BD {nombreBD} eliminada.";
        }
        catch (Exception ex)
        {
            TempData["msg"] = $"No se pudo eliminar: {ex.Message}";
        }

        // Redirigir nuevamente al formulario de subida
        return RedirectToAction("Subir");
    }
}
