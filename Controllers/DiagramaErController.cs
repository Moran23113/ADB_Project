using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Linq;

public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly IRestauracionRepositorio _restaurador;
    private readonly IEsquemaRepositorio _lector;
    private readonly IDiagramaChenRepositorio _diagramaChen;
    private readonly IGeneralizacionEerService _generalizacionService;

    public DiagramaErController(
        IWebHostEnvironment entorno,
        IConfiguration cfg,
        IRestauracionRepositorio restaurador,
        IEsquemaRepositorio lector,
        IDiagramaChenRepositorio diagramaChen,
        IGeneralizacionEerService generalizacionService)
    {
        _entorno = entorno;
        _cfg = cfg;
        _restaurador = restaurador;
        _lector = lector;
        _diagramaChen = diagramaChen;
        _generalizacionService = generalizacionService;
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

        string nombreBD = "";
        try
        {
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");

            var esquema = _lector.Leer(nombreBD);

            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _diagramaChen.Construir(esquema);

            var jerarquias = InferenciaEER.DetectarJerarquias(esquema);

            var csb = new SqlConnectionStringBuilder(_cfg.GetConnectionString("SqlMaestra"));
            csb.InitialCatalog = nombreBD;
            var cnnRestaurada = csb.ToString();

            foreach (var jerarquia in jerarquias)
            {
                var info = _generalizacionService.AnalizarGeneralizacion(
                    cnnRestaurada,
                    jerarquia.Supertipo,
                    jerarquia.Subtipos.ToArray());

                jerarquia.Totalidad = info.EsTotal ? EerTotalness.Total : EerTotalness.Partial;
                jerarquia.Disyuncion = info.EsDisjunta ? EerDisjointness.Exclusive : EerDisjointness.Overlapping;
            }

            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            return View("Resultado");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
        finally
        {
            try { System.IO.File.Delete(rutaBak); } catch { }
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
}
