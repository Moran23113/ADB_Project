using Microsoft.AspNetCore.Mvc;

namespace TuProyecto.Controllers
{
    public class TraductorController : Controller
    {
        private readonly ITraductorRepositorio _repositorioTraductor;

        public TraductorController(ITraductorRepositorio repositorioTraductor)
        {
            _repositorioTraductor = repositorioTraductor;
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
                ? _repositorioTraductor.AlgebraRelacionalASql(texto)
                : _repositorioTraductor.SqlAAlgebraRelacional(texto);

            ViewBag.Resultado = resultado;
            ViewBag.Texto = texto;
            ViewBag.Modo = modo;
            return View("Index");
        }
    }
}
