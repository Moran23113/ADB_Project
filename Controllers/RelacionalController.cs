using Microsoft.AspNetCore.Mvc;

public class RelacionalController : Controller
{
    private readonly IEsquemaRepositorio _lector;
    private readonly IDiagramaRelacionalRepositorio _diagramaRel;

    public RelacionalController(
        IEsquemaRepositorio lector,
        IDiagramaRelacionalRepositorio diagramaRel)
    {
        _lector = lector;
        _diagramaRel = diagramaRel;
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
            var esquema = _lector.Leer(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.MermaidRel = _diagramaRel.Construir(esquema);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando diagrama relacional: " + ex.Message;
            return RedirectToAction("Subir", "DiagramaEr");
        }
    }
}

