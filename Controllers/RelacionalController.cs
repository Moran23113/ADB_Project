using Microsoft.AspNetCore.Mvc;

public class RelacionalController : Controller
{
    private readonly IEsquemaRepositorio _lector;
    private readonly ConstructorDiagramaRelacional _constructorRel;

    public RelacionalController(
        IEsquemaRepositorio lector,
        ConstructorDiagramaRelacional constructorRel)
    {
        _lector = lector;
        _constructorRel = constructorRel;
    }

    /// <summary>
    /// Página principal del Modelo Relacional.
    /// /Relacional?nombreBD=ER_XXXXXXXX
    /// </summary>
    [HttpGet]
    public IActionResult ModeloR(string nombreBD)
    {
        if (string.IsNullOrWhiteSpace(nombreBD))
        {
            TempData["msg"] = "Falta el nombre de la base restaurada.";
            return RedirectToAction("Subir", "DiagramaEr");
        }

        try
        {
            var snap = _lector.Leer(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.MermaidRel = _constructorRel.Construir(snap);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando diagrama relacional: " + ex.Message;
            return RedirectToAction("Subir", "DiagramaEr");
        }
    }
}

