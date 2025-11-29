using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ApplicationSebrae.Controllers
{
    public class TutorialController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Facilitador()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Participante()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Regras()
        {
            return View();
        }
    }
}