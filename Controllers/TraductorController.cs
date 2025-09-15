using Microsoft.AspNetCore.Mvc;

namespace TuProyecto.Controllers
{
    /// <summary>
    /// Controlador que orquesta la interacción del usuario con el traductor AR &lt;-&gt; SQL.
    /// </summary>
    public class TraductorController : Controller
    {
        private readonly ITraductorRepositorio _repositorioTraductor;

        public TraductorController(ITraductorRepositorio repositorioTraductor)
        {
            _repositorioTraductor = repositorioTraductor;
        }

        /// <summary>Renderiza la vista con el formulario del traductor.</summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Evalúa el texto de entrada según el modo seleccionado y devuelve el resultado al usuario.
        /// </summary>
        [HttpPost]
        public IActionResult Traducir(string modo, string texto)
        {
            // Modo AR2SQL interpreta la entrada como álgebra relacional, el otro como SQL.
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
