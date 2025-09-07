using Microsoft.AspNetCore.Mvc;
using TuProyecto.Services;

namespace TuProyecto.Controllers
{
    public class TraductorController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Traducir(string modo, string texto)
        {
            string result;
            if (modo == "AR2SQL")
                result = TraductorSimple.ARtoSQL(texto);
            else
                result = TraductorSimple.SQLtoAR(texto);

            ViewBag.Resultado = result;
            ViewBag.Texto = texto;
            ViewBag.Modo = modo;
            return View("Index");
        }
    }
}
