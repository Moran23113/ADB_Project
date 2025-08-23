using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Linq; // <- asegúrate de tenerlo

public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly ServicioRestauracionSql _restaurador;
    private readonly LectorEsquemaSql _lector;
    private readonly ConstructorDiagramaChen _constructor;

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
            // 1) Restaurar
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");

            // 2) Leer esquema
            var snap = await _lector.LeerAsync(nombreBD);

            // 2.b) Cadena a la BD restaurada (para elecciones EER)
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, nombreBD);

            // 3) ER (Chen)
            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _constructor.Construir(snap);

            // 4) EER: detectar
            var jerarquias = InferenciaEER.DetectarJerarquias(snap);

            // 4.b) Aplicar elecciones guardadas (si existen) desde la BD restaurada
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

            // 4.c) Render EER
            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // 4.d) Ambiguas -> formulario
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
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

    // ====> ACCIÓN SIMPLIFICADA: guarda elecciones en la BD restaurada (sin DbKey)
    [HttpPost]
    [HttpPost]
    public async Task<IActionResult> GuardarChoices(
    string NombreBD, // viene como hidden desde la vista
    Dictionary<string, string> Disyuncion,
    Dictionary<string, string> Totalidad,
    Dictionary<string, string> SubtypesCsv)
    {
        try
        {
            // 1) Guardar elecciones en la BD restaurada
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, NombreBD);
            await EERChoicesRestored.SaveChoicesAsync(cnnRestaurada, Disyuncion, Totalidad, SubtypesCsv);

            // 2) Volver a LEER el esquema y reconstruir los diagramas
            var snap = await _lector.LeerAsync(NombreBD);

            // ER (Chen)
            ViewBag.NombreBD = NombreBD;
            ViewBag.Mermaid = _constructor.Construir(snap);

            // EER: detectar de nuevo
            var jerarquias = InferenciaEER.DetectarJerarquias(snap);

            // Aplicar elecciones guardadas (ya están en la tabla de esa BD)
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

            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // Ya NO deberían quedar ambiguas (o quedarán menos)
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
                .ToList();

            TempData["msg"] = "Elecciones EER guardadas y aplicadas.";
            return View("Resultado"); // <-- nos quedamos en la vista de resultado
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error guardando elecciones: " + ex.Message;
            return RedirectToAction("Subir");
        }
    }
}
    

