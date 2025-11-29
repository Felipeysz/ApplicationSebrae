using Microsoft.AspNetCore.Mvc;
using ApplicationSebrae.Models;
using ApplicationSebrae.Services;
using ApplicationSebrae.ViewModels;

namespace ApplicationSebrae.Controllers
{
    public class RoomController : Controller
    {
        private readonly ILogger<RoomController> _logger;
        private readonly RoomService _roomService;
        private readonly GameService _gameService;

        public RoomController(
            ILogger<RoomController> logger,
            RoomService roomService,
            GameService gameService)
        {
            _logger = logger;
            _roomService = roomService;
            _gameService = gameService;
        }

        // ===== VIEWS =====

        [HttpGet]
        public IActionResult ManageQuestions(string roomCode)
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
            {
                return NotFound("Sala não encontrada");
            }

            var viewModel = new ManageQuestionsViewModel
            {
                RoomCode = roomCode,
                RoomName = room.RoomName,
                CustomDossiers = room.CustomDossiers,
                HasQuestions = room.CustomDossiers.Any(),
                CanStartGame = _roomService.CanStartGame(roomCode)
            };

            return View(viewModel);
        }

        // ===== API ENDPOINTS - GERENCIAMENTO DE PERGUNTAS =====

        [HttpPost]
        public IActionResult AddQuestion([FromBody] AddQuestionRequest request)
        {
            var result = _roomService.AddQuestion(request);
            return Json(new
            {
                success = result.success,
                message = result.message,
                totalQuestions = result.totalQuestions
            });
        }

        [HttpPost]
        public IActionResult EditQuestion([FromBody] EditQuestionRequest request)
        {
            var result = _roomService.EditQuestion(request);
            return Json(new
            {
                success = result.success,
                message = result.message
            });
        }

        [HttpPost]
        public IActionResult DeleteQuestion([FromBody] DeleteQuestionRequest request)
        {
            var result = _roomService.DeleteQuestion(request.RoomCode, request.QuestionIndex);
            return Json(new
            {
                success = result.success,
                message = result.message,
                totalQuestions = result.totalQuestions
            });
        }

        [HttpGet]
        public IActionResult GetQuestions(string roomCode)
        {
            var result = _roomService.GetQuestions(roomCode);
            if (!result.success)
            {
                return Json(new { success = false, message = "Sala não encontrada" });
            }

            return Json(new
            {
                success = true,
                questions = result.questions,
                totalQuestions = result.totalQuestions
            });
        }

        // ===== DEBUG =====

        [HttpGet]
        public IActionResult DebugQuestion(string roomCode, int questionIndex)
        {
            var result = _roomService.DebugQuestion(roomCode, questionIndex);
            if (!result.success)
            {
                return Json(new { success = false, message = "Pergunta não encontrada" });
            }

            return Json(new { success = true, debugInfo = result.debugInfo });
        }
    }
}