using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#region Controlador: Diagrama ER / EER
/// <summary>
/// Controlador MVC para cargar un .bak, generar diagramas ER/EER y manejar elecciones del usuario.
/// </summary>
public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly ServicioRestauracionSql _restaurador;
    private readonly AnalisisBakService _analisis;

    public DiagramaErController(
        IWebHostEnvironment entorno,
        IConfiguration cfg,
        ServicioRestauracionSql restaurador,
        AnalisisBakService analisis)
    {
        _entorno = entorno;
        _cfg = cfg;
        _restaurador = restaurador;
        _analisis = analisis;
    }

    [HttpGet]
    public IActionResult Subir() => View();

    [HttpPost]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Subir(IFormFile archivoBak)
    {
        if (archivoBak == null || archivoBak.Length == 0)
        {
            ModelState.AddModelError("", "Sube un archivo .bak");
            return View();
        }

        var carpeta = _cfg["Upload:Carpeta"] ?? "App_Data/Uploads";
        var raiz = Path.Combine(_entorno.ContentRootPath, carpeta);
        Directory.CreateDirectory(raiz);
        var rutaBak = Path.Combine(raiz, $"{Guid.NewGuid():N}.bak");

        using (var fs = System.IO.File.Create(rutaBak))
            await archivoBak.CopyToAsync(fs);

        try
        {
            var vm = await _analisis.AnalizarBakAsync(rutaBak, _cfg);
            return View("Resultado", vm);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
    }

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
        return RedirectToAction("Subir");
    }

    [HttpPost]
    public async Task<IActionResult> GuardarChoices(
        string NombreBD,
        Dictionary<string, string> Disyuncion,
        Dictionary<string, string> Totalidad,
        Dictionary<string, string> SubtypesCsv)
    {
        try
        {
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, NombreBD);
            await EERChoicesRestored.SaveChoicesAsync(cnnRestaurada, Disyuncion, Totalidad, SubtypesCsv);

            var vm = await _analisis.GenerarDesdeBdAsync(NombreBD, _cfg);
            TempData["msg"] = "Elecciones EER guardadas y aplicadas.";
            return View("Resultado", vm);
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error guardando elecciones: " + ex.Message;
            return RedirectToAction("Subir");
        }
    }
}
#endregion
