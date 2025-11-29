using ApplicationSebrae.Controllers;
using ApplicationSebrae.Models;
using ApplicationSebrae.ViewModels;
using Microsoft.Extensions.Logging;

namespace ApplicationSebrae.Services
{
    public class RoomService
    {
        private readonly ILogger<RoomService> _logger;

        public RoomService(ILogger<RoomService> logger)
        {
            _logger = logger;
        }

        // ===== GERENCIAMENTO DE PERGUNTAS =====

        public (bool success, string message, int totalQuestions) AddQuestion(AddQuestionRequest request)
        {
            try
            {
                var room = RoomManager.GetRoom(request.RoomCode);
                if (room == null)
                    return (false, "Sala não encontrada", 0);

                if (string.IsNullOrWhiteSpace(request.Title))
                    return (false, "Título é obrigatório", 0);

                if (request.Alternatives == null || request.Alternatives.Count < 6)
                    return (false, "É necessário ter pelo menos 6 alternativas", 0);

                if (request.CorrectAnswers == null || !request.CorrectAnswers.Any())
                    return (false, "Selecione pelo menos uma resposta correta", 0);

                if (request.CorrectAnswers.Count > 3)
                    return (false, "Selecione no máximo 3 respostas corretas", 0);

                if (request.CorrectAnswers.Any(idx => idx < 0 || idx >= request.Alternatives.Count))
                    return (false, "Índices de respostas corretas inválidos", 0);

                room.CustomDossiers.Add(new Dossier
                {
                    Title = request.Title,
                    Name = request.Name ?? "",
                    Description = request.Description ?? "",
                    Challenge = request.Challenge ?? "",
                    Objective = request.Objective ?? "",
                    Alternatives = request.Alternatives,
                    CorrectAnswers = request.CorrectAnswers.OrderBy(x => x).ToList(),
                    Explanation = request.Explanation ?? ""
                });

                room.UseCustomDossiers = true;

                _logger.LogInformation($"Pergunta adicionada à sala {request.RoomCode}. Total: {room.CustomDossiers.Count}");
                return (true, "Pergunta adicionada com sucesso!", room.CustomDossiers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar pergunta");
                return (false, "Erro ao adicionar pergunta: " + ex.Message, 0);
            }
        }

        public (bool success, string message) EditQuestion(EditQuestionRequest request)
        {
            try
            {
                var room = RoomManager.GetRoom(request.RoomCode);
                if (room == null)
                    return (false, "Sala não encontrada");

                if (request.QuestionIndex < 0 || request.QuestionIndex >= room.CustomDossiers.Count)
                    return (false, "Pergunta não encontrada");

                if (request.Alternatives == null || request.Alternatives.Count < 6)
                    return (false, "É necessário ter pelo menos 6 alternativas");

                if (request.CorrectAnswers == null || !request.CorrectAnswers.Any())
                    return (false, "Selecione pelo menos uma resposta correta");

                if (request.CorrectAnswers.Count > 3)
                    return (false, "Selecione no máximo 3 respostas corretas");

                var dossier = room.CustomDossiers[request.QuestionIndex];
                dossier.Title = request.Title;
                dossier.Name = request.Name ?? "";
                dossier.Description = request.Description ?? "";
                dossier.Challenge = request.Challenge ?? "";
                dossier.Objective = request.Objective ?? "";
                dossier.Alternatives = request.Alternatives;
                dossier.CorrectAnswers = request.CorrectAnswers.OrderBy(x => x).ToList();
                dossier.Explanation = request.Explanation ?? "";

                _logger.LogInformation($"Pergunta {request.QuestionIndex} editada na sala {request.RoomCode}");
                return (true, "Pergunta atualizada com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao editar pergunta");
                return (false, "Erro ao editar pergunta: " + ex.Message);
            }
        }

        public (bool success, string message, int totalQuestions) DeleteQuestion(string roomCode, int questionIndex)
        {
            try
            {
                var room = RoomManager.GetRoom(roomCode);
                if (room == null)
                    return (false, "Sala não encontrada", 0);

                if (questionIndex < 0 || questionIndex >= room.CustomDossiers.Count)
                    return (false, "Pergunta não encontrada", 0);

                room.CustomDossiers.RemoveAt(questionIndex);

                _logger.LogInformation($"Pergunta {questionIndex} removida da sala {roomCode}. Total restante: {room.CustomDossiers.Count}");
                return (true, "Pergunta removida com sucesso!", room.CustomDossiers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover pergunta");
                return (false, "Erro ao remover pergunta: " + ex.Message, 0);
            }
        }

        public (bool success, object? questions, int totalQuestions) GetQuestions(string roomCode)
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
                return (false, null, 0);

            var questions = room.CustomDossiers.Select((d, idx) => new
            {
                index = idx,
                title = d.Title,
                name = d.Name,
                description = d.Description,
                challenge = d.Challenge,
                objective = d.Objective,
                alternatives = d.Alternatives,
                correctAnswers = d.CorrectAnswers,
                explanation = d.Explanation
            }).ToList();

            return (true, questions, room.CustomDossiers.Count);
        }

        public (bool success, object? debugInfo) DebugQuestion(string roomCode, int questionIndex)
        {
            try
            {
                var room = RoomManager.GetRoom(roomCode);
                if (room == null)
                    return (false, null);

                if (questionIndex < 0 || questionIndex >= room.CustomDossiers.Count)
                    return (false, null);

                var dossier = room.CustomDossiers[questionIndex];

                // Simular randomização para debug
                int seed = room.RoomCode.GetHashCode() + questionIndex;
                var random = new Random(seed);
                var randomized = new List<string>(dossier.Alternatives);

                for (int i = randomized.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (randomized[i], randomized[j]) = (randomized[j], randomized[i]);
                }

                var newCorrectAnswers = new List<int>();
                for (int i = 0; i < randomized.Count; i++)
                {
                    var originalIndex = dossier.Alternatives.IndexOf(randomized[i]);
                    if (dossier.CorrectAnswers.Contains(originalIndex))
                    {
                        newCorrectAnswers.Add(i);
                    }
                }

                var debugInfo = new
                {
                    roomCode,
                    questionIndex,
                    seed,
                    originalAlternatives = dossier.Alternatives,
                    originalCorrectAnswers = dossier.CorrectAnswers,
                    randomizedAlternatives = randomized,
                    newCorrectAnswers,
                    mapping = randomized.Select((alt, idx) => new
                    {
                        newIndex = idx,
                        text = alt,
                        originalIndex = dossier.Alternatives.IndexOf(alt),
                        wasCorrect = dossier.CorrectAnswers.Contains(dossier.Alternatives.IndexOf(alt)),
                        isCorrectNow = newCorrectAnswers.Contains(idx)
                    }).ToList()
                };

                return (true, debugInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao debugar pergunta");
                return (false, null);
            }
        }

        // ===== VALIDAÇÕES =====

        public (bool isValid, string errorMessage) ValidateRoomForGame(string roomCode)
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
                return (false, "Sala não encontrada");

            if (room.UseCustomDossiers && !room.CustomDossiers.Any())
                return (false, "Esta sala usa perguntas personalizadas. Por favor, crie pelo menos uma pergunta antes de iniciar o jogo.");

            return (true, string.Empty);
        }

        public bool CanStartGame(string roomCode)
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
                return false;

            if (room.UseCustomDossiers)
                return room.CustomDossiers.Count >= 6;

            return true; // Dossiers padrão sempre disponíveis
        }
    }
}