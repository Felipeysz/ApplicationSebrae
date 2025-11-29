using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ApplicationSebrae.Models;
using ApplicationSebrae.Services;
using ApplicationSebrae.ViewModels;

namespace ApplicationSebrae.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly SessionManagementService _sessionService;
    private readonly UserManagementService _userService;
    private readonly VotingService _votingService;
    private readonly GameService _gameService;

    public HomeController(
        ILogger<HomeController> logger,
        SessionManagementService sessionService,
        UserManagementService userService,
        VotingService votingService,
        GameService gameService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _userService = userService;
        _votingService = votingService;
        _gameService = gameService;
    }

    // ===== VIEWS PRINCIPAIS =====

    public IActionResult Index() => View();

    public IActionResult FacilitatorLogin() => View();

    [HttpPost]
    public IActionResult FacilitatorLogin(string password)
    {
        if (password == "sebrae2024")
        {
            HttpContext.Session.SetString("UserRole", "Facilitator");
            return RedirectToAction("FacilitatorDashboard");
        }
        TempData["Error"] = "Senha incorreta. Tente novamente.";
        return View();
    }

    public IActionResult FacilitatorDashboard()
    {
        if (!IsFacilitator())
            return RedirectToAction("FacilitatorLogin");

        var model = new FacilitatorDashboardViewModel { ActiveRooms = RoomManager.GetAllRooms() };
        return View(model);
    }

    public IActionResult RoomAccess() => View();

    public IActionResult TeamSelect(string roomCode)
    {
        roomCode = GetRoomCode(roomCode);
        var room = RoomManager.GetRoom(roomCode);

        if (room == null)
            return RedirectToAction("RoomAccess");

        HttpContext.Session.SetString("CurrentRoomCode", roomCode);

        ViewBag.RoomCode = roomCode;
        ViewBag.Teams = room.Teams;
        ViewBag.GameStarted = room.GameStatus == "investigation" || room.GameStatus == "results";
        ViewBag.GameStatus = room.GameStatus;

        return View();
    }

    public IActionResult TeamGame(string roomCode, string teamId)
    {
        roomCode = GetRoomCode(roomCode);
        teamId = GetTeamId(teamId);

        if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(teamId))
            return RedirectToAction("TeamSelect", new { roomCode });

        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return RedirectToAction("RoomAccess");

        var team = room.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null)
            return RedirectToAction("TeamSelect", new { roomCode });

        // Verificar acesso ao time se jogo já iniciou
        if (room.GameStatus == "investigation" || room.GameStatus == "results")
        {
            string userId = HttpContext.Session.GetString($"UserId_{roomCode}_{teamId}") ?? "";
            var (canJoin, message, _, currentTeamId, currentTeamName) = _gameService.CheckTeamAccess(roomCode, teamId, userId, _userService);

            if (!canJoin)
            {
                TempData["Error"] = message;
                return RedirectToAction("TeamSelect", new { roomCode });
            }
        }

        HttpContext.Session.SetString("CurrentRoomCode", roomCode);
        HttpContext.Session.SetString("SelectedTeam", teamId);
        _sessionService.RegisterActiveSession(roomCode, teamId);

        var model = new TeamGameViewModel
        {
            RoomCode = roomCode,
            TeamId = teamId,
            TeamName = team.Name,
            CurrentRound = room.CurrentRound,
            GameStatus = room.GameStatus
        };
        return View(model);
    }

    public IActionResult FacilitatorGame(string roomCode)
    {
        if (!IsFacilitator())
            return RedirectToAction("FacilitatorLogin");

        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return NotFound("Sala não encontrada");

        // Validar se tem perguntas quando usa custom dossiers
        if (room.UseCustomDossiers && !room.CustomDossiers.Any())
        {
            TempData["ErrorMessage"] = "Esta sala usa perguntas personalizadas. Por favor, crie pelo menos uma pergunta antes de iniciar o jogo.";
            return RedirectToAction("ManageQuestions", "Room", new { roomCode });
        }

        var viewModel = new FacilitatorGameViewModel
        {
            RoomCode = roomCode,
            RoomName = room.RoomName,
            CurrentRound = room.CurrentRound,
            TotalRounds = room.UseCustomDossiers ? room.CustomDossiers.Count : 6,
            GameStatus = room.GameStatus,
            Teams = room.Teams,
            ConnectedTeams = _sessionService.GetConnectedTeamsCount(roomCode)
        };

        return View(viewModel);
    }

    public IActionResult TeamDetailedResults(string roomCode, string teamId)
    {
        if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(teamId))
        {
            TempData["Error"] = "Informações da sala ou equipe não encontradas";
            return RedirectToAction("RoomAccess");
        }

        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
        {
            TempData["Error"] = "Sala não encontrada";
            return RedirectToAction("RoomAccess");
        }

        var team = room.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null)
        {
            TempData["Error"] = "Equipe não encontrada";
            return RedirectToAction("TeamSelect", new { roomCode });
        }

        ViewBag.RoomCode = roomCode;
        ViewBag.TeamId = teamId;
        ViewBag.TeamName = team.Name;
        ViewBag.TeamIcon = team.Icon;

        return View();
    }

    // ===== GERENCIAMENTO DE SALAS (FACILITATOR) =====

    [HttpPost]
    public IActionResult CreateRoom([FromBody] CreateRoomViewModel model)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        try
        {
            List<TeamInfo>? customTeams = null;
            if (model.UseCustomTeams && model.CustomTeams.Any())
            {
                customTeams = model.CustomTeams.Select((t, index) => new TeamInfo
                {
                    Id = $"team_{index + 1}",
                    Name = t.Name,
                    Icon = t.Icon,
                    Score = 0
                }).ToList();
            }

            var roomCode = RoomManager.CreateRoom(model.RoomName, customTeams);
            return Json(new { success = true, message = "Sala criada com sucesso!", roomCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar sala");
            return JsonError("Erro ao criar sala");
        }
    }

    [HttpPost]
    public IActionResult ResetRoom([FromBody] RoomAccessViewModel model)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var success = RoomManager.ResetRoom(model.RoomCode);
        if (success)
        {
            _sessionService.RemoveActiveSession(model.RoomCode, "*");
            _userService.KickUser(model.RoomCode, "*");
            _votingService.ClearRoundVotes(model.RoomCode, -1);
        }

        return Json(new { success, message = success ? "Sala resetada com sucesso!" : "Sala não encontrada" });
    }

    [HttpPost]
    public IActionResult DeleteRoom([FromBody] RoomAccessViewModel model)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var success = RoomManager.DeleteRoom(model.RoomCode);
        if (success)
        {
            _sessionService.RemoveActiveSession(model.RoomCode, "*");
            _userService.KickUser(model.RoomCode, "*");
            _votingService.ClearRoundVotes(model.RoomCode, -1);
        }

        return Json(new { success, message = success ? "Sala excluída com sucesso!" : "Sala não encontrada" });
    }

    [HttpPost]
    public IActionResult ResetAllRooms()
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        RoomManager.ResetAllRooms();
        _sessionService.ClearAllSessions();
        _userService.ClearAllUsers();
        _votingService.ClearAllVotes();

        return Json(new { success = true, message = "Todas as salas foram resetadas!" });
    }

    [HttpPost]
    public IActionResult ResetGame()
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        _sessionService.ClearAllSessions();
        _userService.ClearAllUsers();
        _votingService.ClearAllVotes();
        HttpContext.Session.Clear();

        return Json(new { success = true, message = "Jogo reiniciado com sucesso!" });
    }

    [HttpPost]
    public IActionResult ResetRound([FromBody] RoomAccessViewModel model)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var result = _gameService.ResetCurrentRound(model.RoomCode, _userService);

        var room = RoomManager.GetRoom(model.RoomCode);
        return Json(new
        {
            result.success,
            result.message,
            currentRound = room?.CurrentRound ?? 0,
            gameStatus = room?.GameStatus ?? "error",
            resetTime = room?.LastResetTime
        });
    }

    // ===== ACESSO E VERIFICAÇÃO DE SALAS =====

    [HttpPost]
    public IActionResult VerifyRoomCode([FromBody] RoomAccessViewModel model)
    {
        if (RoomManager.RoomExists(model.RoomCode))
        {
            HttpContext.Session.SetString("CurrentRoomCode", model.RoomCode);
            return Json(new { success = true });
        }
        return JsonError("Código inválido. Verifique e tente novamente.");
    }

    [HttpGet]
    public IActionResult GetActiveSessions(string roomCode) =>
        Json(new { connectedTeams = _sessionService.GetConnectedTeamsCount(roomCode) });

    [HttpPost]
    public IActionResult TeamDisconnect([FromBody] TeamDisconnectViewModel model)
    {
        _sessionService.RemoveActiveSession(model.RoomCode, model.TeamId);
        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult TeamPing([FromBody] TeamPingViewModel model)
    {
        if (!string.IsNullOrEmpty(model.RoomCode) && !string.IsNullOrEmpty(model.TeamId))
        {
            _sessionService.RegisterActiveSession(model.RoomCode, model.TeamId);
            return Json(new { success = true });
        }
        return Json(new { success = false });
    }

    // ===== GERENCIAMENTO DE USUÁRIOS =====

    [HttpPost]
    public IActionResult JoinTeam([FromBody] JoinTeamViewModel model)
    {
        var room = RoomManager.GetRoom(model.RoomCode);
        if (room == null)
            return JsonError("Sala não encontrada");

        // Verificar acesso ao time
        var (canJoin, message, isReturning, currentTeamId, currentTeamName) =
            _gameService.CheckTeamAccess(model.RoomCode, model.TeamId, model.UserId, _userService);

        if (!canJoin)
        {
            return Json(new
            {
                success = false,
                message,
                currentTeamId,
                currentTeamName,
                shouldRedirect = currentTeamId != null,
                gameInProgress = currentTeamId == null
            });
        }

        // Se pode entrar, processar entrada
        if (isReturning)
        {
            var user = _userService.GetAllUsersInRoom(model.RoomCode)
                .FirstOrDefault(u => u.Id == model.UserId);

            if (user != null)
            {
                user.IsConnected = true;
                user.LastActivity = DateTime.Now;

                return Json(new
                {
                    success = true,
                    userId = user.Id,
                    isNewUser = false,
                    isLeader = user.IsLeader,
                    message
                });
            }
        }

        var result = _userService.JoinTeam(model);
        return Json(new { result.success, result.userId, result.isNewUser, result.isLeader, result.message });
    }

    [HttpGet]
    public IActionResult CheckTeamAccess(string roomCode, string teamId, string userId)
    {
        var (canJoin, message, isReturning, currentTeamId, currentTeamName) =
            _gameService.CheckTeamAccess(roomCode, teamId, userId, _userService);

        var room = RoomManager.GetRoom(roomCode);

        return Json(new
        {
            success = canJoin,
            canJoin,
            isReturning,
            message,
            gameStatus = room?.GameStatus ?? "error",
            userCurrentTeam = currentTeamId ?? "none",
            currentTeamName = currentTeamName ?? ""
        });
    }

    [HttpPost]
    public IActionResult UpdateUserName([FromBody] UpdateUserNameViewModel model)
    {
        var result = _userService.UpdateUserName(model);
        return Json(new { result.success, result.message, result.userName });
    }

    [HttpGet]
    public IActionResult GetTeamUsers(string roomCode, string teamId)
    {
        var users = _userService.GetTeamUsers(roomCode, teamId)
            .Select(u => new {
                Id = u.Id ?? string.Empty,
                Name = u.Name ?? "Jogador",
                u.HasVoted,
                u.IsConnected,
                u.LastActivity,
                u.MissedVotes,
                IsMe = false
            })
            .Where(u => !string.IsNullOrEmpty(u.Id))
            .ToList();

        return Json(new { success = true, users });
    }

    [HttpPost]
    public IActionResult KickUser([FromBody] KickUserViewModel model)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var result = _userService.KickUser(model.RoomCode, model.UserId);
        return Json(new { result.success, result.message });
    }

    // ===== SISTEMA DE VOTAÇÃO =====

    [HttpPost]
    public IActionResult SubmitVote([FromBody] VoteSubmissionViewModel submission)
    {
        var result = _votingService.SubmitVote(submission, _userService);
        return Json(new { result.success, result.message, result.hasVoted, result.voteCount });
    }


    [HttpGet]
    public IActionResult GetTeamVotingStatus(string roomCode, string teamId) =>
        Json(_gameService.GetTeamVotingStatus(roomCode, teamId, _userService));

    [HttpGet]
    public IActionResult GetVoteDistribution(string roomCode, string teamId)
    {
        try
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
                return JsonError("Sala não encontrada");

            var distribution = _votingService.GetVoteDistribution(roomCode, teamId, room.CurrentRound, _userService);
            return Json(new { success = true, distribution, totalVotes = distribution.Values.Sum() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter distribuição de votos");
            return JsonError("Erro ao obter distribuição de votos");
        }
    }

    // ===== JOGO - DOSSIERS E RODADAS =====

    [HttpGet]
    public IActionResult GetDossier(string roomCode, int round) =>
        Json(_gameService.GetDossier(roomCode, round));

    [HttpPost]
    public IActionResult StartInvestigation(string roomCode)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var result = _gameService.StartInvestigation(roomCode, _sessionService);
        return Json(new { result.success, result.message, result.gameStatus, result.connectedTeams });
    }

    [HttpPost]
    public IActionResult ShowResults(string roomCode)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var result = _gameService.ShowResults(roomCode);
        return Json(new { result.success, result.message, result.gameStatus, result.correctAnswers, result.explanation });
    }

    [HttpPost]
    public IActionResult NextRound(string roomCode)
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var result = _gameService.NextRound(roomCode, _sessionService);
        return Json(new { result.success, result.message, result.shouldReload, result.finished });
    }

    // ===== SUBMISSÃO DE RESPOSTAS =====

    [HttpPost]
    public IActionResult SubmitTeamAnswer([FromBody] TeamAnswerRequestViewModel request)
    {
        try
        {
            var result = _gameService.SubmitTeamAnswer(request, _userService, _sessionService);
            var room = RoomManager.GetRoom(request.RoomCode);
            var team = room?.Teams.FirstOrDefault(t => t.Id == request.TeamId);

            // ✅ Obter informações sobre os votos (para estatísticas)
            var distribution = _votingService.GetVoteDistribution(request.RoomCode, request.TeamId, request.Round, _userService);
            var teamVotes = _votingService.GetTeamVotes(request.RoomCode, request.TeamId, request.Round, _userService);
            var totalVoters = teamVotes.Count;

            // ✅ Obter alternativas mais votadas (para feedback visual, não para pontuação)
            var mostVotedAlternatives = distribution
                .OrderByDescending(kvp => kvp.Value)
                .Take(2)
                .Select(kvp => kvp.Key)
                .ToList();

            return Json(new
            {
                success = result.success,
                correct = result.isCorrect,
                points = result.score,
                totalScore = team?.Score ?? 0,
                totalVoters,
                voteDistribution = distribution,
                mostVotedAlternatives, // ✅ Para feedback visual apenas
                message = result.message,
                allTeamsResponded = result.allTeamsResponded,
                advancedToNextRound = result.advancedToNextRound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao submeter resposta da equipe");
            return JsonError("Erro ao submeter resposta");
        }
    }

    [HttpGet]
    public IActionResult CheckTeamSubmission(string roomCode, string teamId)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return JsonError("Sala não encontrada");

        var team = room.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null)
            return JsonError("Time não encontrado");

        bool hasSubmitted = team.Responses.ContainsKey(room.CurrentRound);

        return Json(new
        {
            success = true,
            hasSubmitted,
            currentRound = room.CurrentRound,
            gameStatus = room.GameStatus
        });
    }

    // ===== ESTADO E RESULTADOS DO JOGO =====

    [HttpGet]
    public IActionResult GetGameState(string roomCode) =>
        Json(_gameService.GetGameState(roomCode, _sessionService));

    [HttpGet]
    public IActionResult GetFinalResults(string roomCode) =>
        Json(_gameService.GetFinalResults(roomCode, _sessionService));

    [HttpGet]
    public IActionResult GetTeamDetailedResults(string roomCode, string teamId)
    {
        try
        {
            return Json(_gameService.GetTeamDetailedResults(roomCode, teamId, _userService));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter resultados detalhados");
            return JsonError("Erro ao carregar resultados");
        }
    }

    [HttpGet]
    public IActionResult GetDashboardData()
    {
        if (!IsFacilitator())
            return JsonError("Acesso negado");

        var activeRooms = RoomManager.GetAllRooms();

        var stats = new
        {
            TotalRooms = activeRooms.Count,
            ActiveGames = activeRooms.Count(r => r.GameStatus != "setup" && r.GameStatus != "finished"),
            TotalPlayers = activeRooms.Sum(r => _sessionService.GetConnectedTeamsCount(r.RoomCode))
        };

        var roomsData = activeRooms.Select(room => new
        {
            room.RoomCode,
            room.RoomName,
            ConnectedTeams = _sessionService.GetConnectedTeamsCount(room.RoomCode),
            TotalTeams = room.Teams.Count,
            room.GameStatus,
            room.CurrentRound,
            TotalRounds = room.UseCustomDossiers ? room.CustomDossiers.Count : _gameService.GetGameDossiers().Count
        }).ToList();

        return Json(new { success = true, activeRooms = roomsData, stats });
    }

    // ===== MÉTODOS AUXILIARES =====

    private bool IsFacilitator() =>
        HttpContext.Session.GetString("UserRole") == "Facilitator";

    private string GetRoomCode(string? roomCode) =>
        string.IsNullOrEmpty(roomCode)
            ? HttpContext.Session.GetString("CurrentRoomCode") ?? ""
            : roomCode;

    private string GetTeamId(string? teamId) =>
        string.IsNullOrEmpty(teamId)
            ? HttpContext.Session.GetString("SelectedTeam") ?? ""
            : teamId;

    private JsonResult JsonError(string message) =>
        Json(new { success = false, message });

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}