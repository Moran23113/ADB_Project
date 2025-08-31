using Microsoft.AspNetCore.Mvc;

public class RelacionalController : Controller
{
    private readonly LectorEsquemaSql _lector;
    private readonly ConstructorDiagramaRelacional _renderizador;

    public RelacionalController(
        LectorEsquemaSql lector,
        ConstructorDiagramaRelacional renderizador)
    {
        _lector = lector;
        _renderizador = renderizador;
    }

    /// <summary>
    /// Página principal del Modelo Relacional.
    /// /Relacional?nombreBD=ER_XXXXXXXX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string nombreBD)
    {
        if (string.IsNullOrWhiteSpace(nombreBD))
        {
            TempData["msg"] = "Falta el nombre de la base restaurada.";
            return RedirectToAction("Subir", "DiagramaEr");
        }

        try
        {
            var snap = await _lector.LeerAsync(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.MermaidRel = _renderizador.Construir(snap);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando diagrama relacional: " + ex.Message;
            return RedirectToAction("Subir", "DiagramaEr");
        }
    }
}

