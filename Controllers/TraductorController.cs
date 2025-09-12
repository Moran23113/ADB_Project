using Microsoft.AspNetCore.Mvc;

namespace TuProyecto.Controllers
{
    public class TraductorController : Controller
    {
        private readonly ITraductorRelacional traductor;

        public TraductorController(ITraductorRelacional traductor)
        {
            this.traductor = traductor;
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
                ? traductor.AlgebraRelacionalASql(texto)
                : traductor.SqlAAlgebraRelacional(texto);

            ViewBag.Resultado = resultado;
            ViewBag.Texto = texto;
            ViewBag.Modo = modo;
            return View("Index");
        }
    }
}
