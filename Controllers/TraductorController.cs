using Microsoft.AspNetCore.Mvc;

namespace TuProyecto.Controllers
{
    public class TraductorController : Controller
    {
        private readonly ITraductorRepositorio _traductor;

        public TraductorController(ITraductorRepositorio traductor)
        {
            _traductor = traductor;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Traducir(string modo, string texto)
        {
            string resultado = modo == "AR2SQL"
                ? _traductor.AlgebraRelacionalASql(texto)
                : _traductor.SqlAAlgebraRelacional(texto);

            ViewBag.Resultado = resultado;
            ViewBag.Texto = texto;
            ViewBag.Modo = modo;
            return View("Index");
        }
    }
}
