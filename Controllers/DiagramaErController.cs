using Microsoft.AspNetCore.Mvc;
using System.Linq;


public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly IRestauracionRepositorio _restaurador;
    private readonly IEsquemaRepositorio _lector;
    private readonly IDiagramaChenRepositorio _diagramaChen;


 
    public DiagramaErController(
        IWebHostEnvironment entorno,
        IConfiguration cfg,
        IRestauracionRepositorio restaurador,
        IEsquemaRepositorio lector,
        IDiagramaChenRepositorio diagramaChen)
    {
        _entorno = entorno;
        _cfg = cfg;
        _restaurador = restaurador;
        _lector = lector;
        _diagramaChen = diagramaChen;
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
            await AplicarEleccionesAsync(nombreBD, jerarquias);

            
            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(jerarquia => jerarquia.Disyuncion == EerDisjointness.Ambiguous || jerarquia.Totalidad == EerTotalness.Ambiguous)
                .ToList();

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

    
    
    
    private async Task AplicarEleccionesAsync(string nombreBD, List<JerarquiaEer> jerarquias)
    {
        var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, nombreBD);
        var choices = await EERChoicesRestored.LoadChoicesAsync(cnnRestaurada);

        foreach (var jerarquia in jerarquias)
        {
            var subsCsv = string.Join(",", jerarquia.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var eleccion = choices.FirstOrDefault(x =>
                x.sup.Equals(jerarquia.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(eleccion.sup))
            {
                jerarquia.Disyuncion = eleccion.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                    ? EerDisjointness.Exclusive : EerDisjointness.Overlapping;
                jerarquia.Totalidad = eleccion.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                    ? EerTotalness.Total : EerTotalness.Partial;
                jerarquia.Evidencia = "Elección del usuario aplicada.";
            }
        }
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

            
            var esquema = _lector.Leer(NombreBD);

            ViewBag.NombreBD = NombreBD;
            ViewBag.Mermaid = _diagramaChen.Construir(esquema);

            var jerarquias = InferenciaEER.DetectarJerarquias(esquema);
            await AplicarEleccionesAsync(NombreBD, jerarquias);

            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(jerarquia => jerarquia.Disyuncion == EerDisjointness.Ambiguous || jerarquia.Totalidad == EerTotalness.Ambiguous)
                .ToList();

            TempData["msg"] = "Elecciones EER guardadas y aplicadas.";
            return View("Resultado");
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error guardando elecciones: " + ex.Message;
            return RedirectToAction("Subir");
        }
    }
}
