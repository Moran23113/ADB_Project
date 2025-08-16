using Microsoft.AspNetCore.Mvc;

public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly ServicioRestauracionSql _restaurador;
    private readonly LectorEsquemaSql _lector;
    private readonly ConstructorDiagramaChen _constructor;

    public DiagramaErController(IWebHostEnvironment entorno, IConfiguration cfg,
        ServicioRestauracionSql restaurador, LectorEsquemaSql lector, ConstructorDiagramaChen constructor)
    { _entorno = entorno; _cfg = cfg; _restaurador = restaurador; _lector = lector; _constructor = constructor; }

    [HttpGet] public IActionResult Subir() => View();

    [HttpPost]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Subir(IFormFile archivoBak)
    {
        if (archivoBak == null || archivoBak.Length == 0) { ModelState.AddModelError("", "Sube un archivo .bak"); return View(); }

        var carpeta = _cfg["Upload:Carpeta"] ?? "App_Data/Uploads";

        var raiz = Path.Combine(_entorno.ContentRootPath, carpeta);
        Directory.CreateDirectory(raiz);
        var rutaBak = Path.Combine(raiz, $"{Guid.NewGuid():N}.bak");
        using (var fs = System.IO.File.Create(rutaBak)) await archivoBak.CopyToAsync(fs);

        string nombreBD = "";
        try
        {
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");
            var snap = await _lector.LeerAsync(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _constructor.Construir(snap);
            return View("Resultado");
        }
        catch (Exception ex) { ModelState.AddModelError("", $"Error: {ex.Message}"); return View(); }
        finally { try { System.IO.File.Delete(rutaBak); } catch { } }
    }

    [HttpPost]
    public async Task<IActionResult> EliminarBase(string nombreBD)
    {
        try { await _restaurador.EliminarBaseDatosAsync(nombreBD); TempData["msg"] = $"BD {nombreBD} eliminada."; }
        catch (Exception ex) { TempData["msg"] = $"No se pudo eliminar: {ex.Message}"; }
        return RedirectToAction("Subir");
    }
}
