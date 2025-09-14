using Microsoft.AspNetCore.Mvc;

public class RelacionalController : Controller
{
    private readonly IEsquemaRepositorio _repositorioEsquema;
    private readonly IModeloRelacionalTextoRepositorio _repositorioModeloRelacional;

    public RelacionalController(
        IEsquemaRepositorio repositorioEsquema,
        IModeloRelacionalTextoRepositorio repositorioModeloRelacional)
    {
        _repositorioEsquema = repositorioEsquema;
        _repositorioModeloRelacional = repositorioModeloRelacional;
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
            var esquema = _repositorioEsquema.Leer(nombreBD);
            ViewBag.NombreBD = nombreBD;
            ViewBag.ModeloRelacional = _repositorioModeloRelacional.Construir(esquema);
            return View();
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error generando modelo relacional: " + ex.Message;
            return RedirectToAction("Subir", "DiagramaEr");
        }
    }
}

