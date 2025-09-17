using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Controlador que gestiona la restauración y generación del modelo relacional textual.
/// </summary>
public class RelacionalController : Controller
{
    private readonly IWebHostEnvironment _entornoWeb;
    private readonly IConfiguration _configuracion;
    private readonly IRestauracionRepositorio _repositorioRestauracion;
    private readonly IEsquemaRepositorio _repositorioEsquema;
    private readonly IModeloRelacionalTextoRepositorio _repositorioModeloRelacional;

    public RelacionalController(
        IWebHostEnvironment entornoWeb,
        IConfiguration configuracion,
        IRestauracionRepositorio repositorioRestauracion,
        IEsquemaRepositorio repositorioEsquema,
        IModeloRelacionalTextoRepositorio repositorioModeloRelacional)
    {
        _entornoWeb = entornoWeb;
        _configuracion = configuracion;
        _repositorioRestauracion = repositorioRestauracion;
        _repositorioEsquema = repositorioEsquema;
        _repositorioModeloRelacional = repositorioModeloRelacional;
    }

    /// <summary>Muestra la vista para subir el respaldo.</summary>
    [HttpGet]
    public IActionResult Subir() => View();

    /// <summary>
    /// Restaura el respaldo con un prefijo específico y redirige a la vista del modelo relacional.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Subir(IFormFile archivoRespaldo)
    {
        if (archivoRespaldo == null || archivoRespaldo.Length == 0)
        {
            ModelState.AddModelError("", "Sube un archivo .bak");
            return View();
        }

        // Guarda el archivo de respaldo en una ubicación temporal.
        var carpetaSubida = _configuracion["Upload:Carpeta"] ?? "App_Data/Uploads";
        var rutaCarpetaSubida = Path.Combine(_entornoWeb.ContentRootPath, carpetaSubida);
        Directory.CreateDirectory(rutaCarpetaSubida);

        var rutaRespaldo = Path.Combine(rutaCarpetaSubida, $"{Guid.NewGuid():N}.bak");

        // Copia el contenido subido al archivo temporal.
        using (var archivo = System.IO.File.Create(rutaRespaldo))
            await archivoRespaldo.CopyToAsync(archivo);

        string nombreBaseRestaurada = string.Empty;
        try
        {
            // Restaurar la base con prefijo REL para diferenciarla de las usadas en los diagramas.
            nombreBaseRestaurada = await _repositorioRestauracion.RestaurarAsync(rutaRespaldo, "REL");
            return RedirectToAction("ModeloR", new { nombreBD = nombreBaseRestaurada });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
        finally
        {
            // El archivo temporal se elimina siempre que sea posible.
            try { System.IO.File.Delete(rutaRespaldo); } catch { }
        }
    }

    /// <summary>
    /// Lee el esquema de la base restaurada y construye la salida textual del modelo relacional.
    /// </summary>
    [HttpGet]
    public IActionResult ModeloR(string nombreBD)
    {
        if (string.IsNullOrWhiteSpace(nombreBD))
        {
            TempData["msg"] = "Falta el nombre de la base restaurada.";
            return RedirectToAction("Subir");
        }

        try
        {
            var esquema = _repositorioEsquema.Leer(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.ModeloRelacional = _repositorioModeloRelacional.Construir(esquema);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando modelo relacional: " + ex.Message;
            return RedirectToAction("Subir");
        }
    }

    /// <summary>Permite limpiar manualmente la base restaurada una vez generada la documentación.</summary>
    [HttpPost]
    public async Task<IActionResult> EliminarBase(string nombreBD)
    {
        try
        {
            await _repositorioRestauracion.EliminarBaseDatosAsync(nombreBD);
            TempData["msg"] = $"BD {nombreBD} eliminada.";
        }
        catch (Exception ex)
        {
            TempData["msg"] = $"No se pudo eliminar: {ex.Message}";
        }
        return RedirectToAction("Subir");
    }
}

