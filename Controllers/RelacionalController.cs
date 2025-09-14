using Microsoft.AspNetCore.Mvc;

public class RelacionalController : Controller
{
    private readonly IEsquemaRepositorio _lector;
    private readonly IModeloRelacionalTextoRepositorio _modeloRel;

    public RelacionalController(
        IEsquemaRepositorio lector,
        IModeloRelacionalTextoRepositorio modeloRel)
    {
        _lector = lector;
        _modeloRel = modeloRel;
    }

    
    
    
    
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
            ViewBag.ModeloRelacional = _modeloRel.Construir(esquema);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando modelo relacional: " + ex.Message;
            return RedirectToAction("Subir", "DiagramaEr");
        }
    }
}

