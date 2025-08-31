using System.Diagnostics;
using ABD_Project.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace ABD_Project.Controladores
{
    public class InicioController : Controller
    {
        private readonly ILogger<InicioController> _logger;

        public InicioController(ILogger<InicioController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacidad()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ModeloVistaError { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
