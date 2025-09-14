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
        public IActionResult Traducir(string modo, string textoEntrada)
        {
            string resultado = modo == "AR2SQL"
                ? _repositorioTraductor.AlgebraRelacionalASql(textoEntrada)
                : _repositorioTraductor.SqlAAlgebraRelacional(textoEntrada);

            ViewBag.Resultado = resultado;
            ViewBag.Texto = textoEntrada;
            ViewBag.Modo = modo;
            return View("Index");
        }
    }
}
