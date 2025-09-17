using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Linq;

public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entornoWeb;
    private readonly IConfiguration _configuracion;
    private readonly IRestauracionRepositorio _repositorioRestauracion;
    private readonly IEsquemaRepositorio _repositorioEsquema;
    private readonly IDiagramaChenRepositorio _repositorioDiagramaChen;
    private readonly IEspecializacionEerService _servicioEspecializacion;

    public DiagramaErController(
        IWebHostEnvironment entornoWeb,
        IConfiguration configuracion,
        IRestauracionRepositorio repositorioRestauracion,
        IEsquemaRepositorio repositorioEsquema,
        IDiagramaChenRepositorio repositorioDiagramaChen,
        IEspecializacionEerService servicioEspecializacion)
    {
        _entornoWeb = entornoWeb;
        _configuracion = configuracion;
        _repositorioRestauracion = repositorioRestauracion;
        _repositorioEsquema = repositorioEsquema;
        _repositorioDiagramaChen = repositorioDiagramaChen;
        _servicioEspecializacion = servicioEspecializacion;
    }

    [HttpGet]
    public IActionResult Subir() => View();

    [HttpPost]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> Subir(IFormFile archivoRespaldo)
    {
        if (archivoRespaldo == null
     || archivoRespaldo.Length == 0
     || !string.Equals(Path.GetExtension(archivoRespaldo.FileName), ".bak", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("", "Sube un archivo .bak válido");
            return View();
        }

        var carpetaSubida = _configuracion["Upload:Carpeta"] ?? "App_Data/Uploads";
        var rutaCarpetaSubida = Path.Combine(_entornoWeb.ContentRootPath, carpetaSubida);
        Directory.CreateDirectory(rutaCarpetaSubida);

        var rutaRespaldo = Path.Combine(rutaCarpetaSubida, $"{Guid.NewGuid():N}.bak");

        using (var archivo = System.IO.File.Create(rutaRespaldo))
            await archivoRespaldo.CopyToAsync(archivo);

        string nombreBaseRestaurada = string.Empty;
        try
        {
            nombreBaseRestaurada = await _repositorioRestauracion.RestaurarAsync(rutaRespaldo, "ER");

            var esquema = _repositorioEsquema.Leer(nombreBaseRestaurada);

            ViewBag.NombreBD = nombreBaseRestaurada;
            ViewBag.Mermaid = _repositorioDiagramaChen.Construir(esquema);

            var jerarquias = InferenciaEER.DetectarJerarquias(esquema);

            var constructorCadena = new SqlConnectionStringBuilder(_configuracion.GetConnectionString("SqlMaestra"))
            {
                InitialCatalog = nombreBaseRestaurada
            };
            var cadenaConexionRestaurada = constructorCadena.ToString();

            foreach (var jerarquia in jerarquias)
            {
                var infoEspecializacion = _servicioEspecializacion.AnalizarEspecializacion(
                    cadenaConexionRestaurada,
                    jerarquia.Supertipo,
                    jerarquia.Subtipos.ToArray());

                jerarquia.Totalidad = infoEspecializacion.EsTotal ? EerTotalness.Total : EerTotalness.Parcial;
                jerarquia.Disyuncion = infoEspecializacion.EsDisjunta ? EerDisjointness.Exclusiva : EerDisjointness.Solapada;
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
            try { System.IO.File.Delete(rutaRespaldo); } catch { }
        }
    }

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
