using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Linq;

/// <summary>
/// Controlador encargado del flujo de restaurar un respaldo, leer su esquema y generar los diagramas ER/EER.
/// </summary>
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

    /// <summary>Muestra el formulario para cargar el respaldo de base de datos.</summary>
    [HttpGet]
    public IActionResult Subir() => View();

    /// <summary>
    /// Recibe el archivo <c>.bak</c>, lo restaura temporalmente y construye los diagramas ER/EER.
    /// </summary>
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

        // Determina la carpeta donde se almacenará temporalmente el respaldo antes de restaurarlo.
        var carpetaSubida = _configuracion["Upload:Carpeta"] ?? "App_Data/Uploads";
        var rutaCarpetaSubida = Path.Combine(_entornoWeb.ContentRootPath, carpetaSubida);
        Directory.CreateDirectory(rutaCarpetaSubida);

        var rutaRespaldo = Path.Combine(rutaCarpetaSubida, $"{Guid.NewGuid():N}.bak");

        // Se guarda el archivo cargado en disco para que SMO pueda consumirlo.
        using (var archivo = System.IO.File.Create(rutaRespaldo))
            await archivoRespaldo.CopyToAsync(archivo);

        string nombreBaseRestaurada = string.Empty;
        try
        {
            // 1. Restaurar el respaldo en SQL Server usando un prefijo identificable.
            nombreBaseRestaurada = await _repositorioRestauracion.RestaurarAsync(rutaRespaldo, "ER");

            // 2. Leer el esquema (tablas, columnas y relaciones) de la base restaurada.
            var esquema = _repositorioEsquema.Leer(nombreBaseRestaurada);

            ViewBag.NombreBD = nombreBaseRestaurada;
            // 3. Construir el diagrama Chen en formato Mermaid.
            ViewBag.Mermaid = _repositorioDiagramaChen.Construir(esquema);

            // 4. Inferir jerarquías para el diagrama EER.
            var jerarquias = InferenciaEER.DetectarJerarquias(esquema);

            var constructorCadena = new SqlConnectionStringBuilder(_configuracion.GetConnectionString("SqlMaestra"))
            {
                InitialCatalog = nombreBaseRestaurada
            };
            var cadenaConexionRestaurada = constructorCadena.ToString();

            foreach (var jerarquia in jerarquias)
            {
                // Consulta la especialización directamente en la base restaurada para determinar totalidad y disyunción.
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
            // Siempre se intenta borrar el archivo físico para no dejar residuos en disco.
            try { System.IO.File.Delete(rutaRespaldo); } catch { }
        }
    }

    /// <summary>Permite eliminar manualmente la base temporal creada tras la restauración.</summary>
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
