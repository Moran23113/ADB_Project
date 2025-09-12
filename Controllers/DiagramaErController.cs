using Microsoft.AspNetCore.Mvc;
using System.Linq;

#region Controlador: Diagrama ER / EER

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
    [RequestSizeLimit(1_000_000_000)] // 1 GB (ajustar según política)
    public async Task<IActionResult> Subir(IFormFile archivoBak)
    {
        // Validación de entrada.
        if (archivoBak == null || archivoBak.Length == 0)
        {
            ModelState.AddModelError("", "Sube un archivo .bak");
            return View();
        }

        // Carpeta destino para almacenar temporalmente el .bak subido.
        var carpeta = _cfg["Upload:Carpeta"] ?? "App_Data/Uploads";
        var raiz = Path.Combine(_entorno.ContentRootPath, carpeta);
        Directory.CreateDirectory(raiz);

        // Nombre único del .bak en disco.
        var rutaBak = Path.Combine(raiz, $"{Guid.NewGuid():N}.bak");

        // Guardar el archivo a disco.
        using (var fs = System.IO.File.Create(rutaBak))
            await archivoBak.CopyToAsync(fs);

        string nombreBD = "";
        try
        {
            // 1) Restaurar la BD desde el .bak (nombre aleatorio prefijado con "ER").
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");

            // 2) Leer el esquema de la BD restaurada (tablas, columnas, PKs, FKs...).
            var snap = _lector.Leer(nombreBD);

            // 3) ER (Chen): generar Mermaid del modelo relacional.
            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _diagramaChen.Construir(snap);

            // 4) EER: detectar jerarquías de subtipos mediante heurística PK=FK (FK UNIQUE).
            var jerarquias = InferenciaEER.DetectarJerarquias(snap);
            await AplicarEleccionesAsync(nombreBD, jerarquias);

            // 4.c) Render EER en Mermaid (con etiqueta "especializacion").
            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // 4.d) Filtrar jerarquías que siguen ambiguas (mostrar formulario para resolver).
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
                .ToList();

            return View("Resultado");
        }
        catch (Exception ex)
        {
            // Nota: evitar revelar detalles sensibles (rutas, cadenas, etc.) en producción.
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
        finally
        {
            // Limpieza: eliminar el .bak temporal.
            try { System.IO.File.Delete(rutaBak); } catch { /* swallow logging si aplica */ }
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

    /// <summary>
    /// Aplica elecciones guardadas de disyunción/totalidad a las jerarquías detectadas.
    /// </summary>
    private async Task AplicarEleccionesAsync(string nombreBD, List<JerarquiaEer> jerarquias)
    {
        var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, nombreBD);
        var choices = await EERChoicesRestored.LoadChoicesAsync(cnnRestaurada);

        foreach (var j in jerarquias)
        {
            var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var c = choices.FirstOrDefault(x =>
                x.sup.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(c.sup))
            {
                j.Disyuncion = c.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                    ? EerDisjointness.Exclusive : EerDisjointness.Overlapping;
                j.Totalidad = c.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                    ? EerTotalness.Total : EerTotalness.Partial;
                j.Evidencia = "Elección del usuario aplicada.";
            }
        }
    }

    /// <summary>
    /// Guarda las elecciones del usuario para Disyunción/Totalidad de cada jerarquía EER
    /// directamente en la BD restaurada y re-renderiza los diagramas.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuardarChoices(
        string NombreBD,
        Dictionary<string, string> Disyuncion,
        Dictionary<string, string> Totalidad,
        Dictionary<string, string> SubtypesCsv)
    {
        try
        {
            // 1) Guardar elecciones en la BD restaurada (tabla interna de choices).
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, NombreBD);
            await EERChoicesRestored.SaveChoicesAsync(cnnRestaurada, Disyuncion, Totalidad, SubtypesCsv);

            // 2) Releer esquema y reconstruir diagramas (ER + EER) tras aplicar elecciones.
            var snap = _lector.Leer(NombreBD);

            ViewBag.NombreBD = NombreBD;
            ViewBag.Mermaid = _diagramaChen.Construir(snap);

            var jerarquias = InferenciaEER.DetectarJerarquias(snap);
            await AplicarEleccionesAsync(NombreBD, jerarquias);

            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // Idealmente ya no hay ambigüedades; si quedan, se vuelven a mostrar.
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
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
#endregion
