using System.Diagnostics;
using ABD_Project.Models;
using Microsoft.AspNetCore.Mvc;

namespace ABD_Project.Controllers
{
    // Controlador para las páginas de inicio y privacidad.
    public class HomeController : Controller
    {
        // Registrador para escribir eventos en el log.
        private readonly ILogger<HomeController> _logger;

        // El registrador se inyecta a través del constructor.
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger; // Se almacena para su uso posterior.
        }

        // Acción que muestra la vista principal.
        public IActionResult Index()
        {
            return View(); // Renderiza Views/Home/Index.cshtml.
        }

        // Acción que muestra la vista de política de privacidad.
        public IActionResult Privacy()
        {
            return View(); // Renderiza Views/Home/Privacy.cshtml.
        }

        // Acción llamada cuando ocurre un error no controlado.
        // La respuesta no se almacena en caché para evitar datos obsoletos.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Se pasa un modelo con el identificador de la solicitud actual.
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}