using ApplicationSebrae.Models;
using ApplicationSebrae.ViewModels;
using Microsoft.Extensions.Logging;

namespace ApplicationSebrae.Services;

public class GameService(ILogger<GameService> logger, VotingService votingService)
{
    // ===== RANDOMIZAÇÃO CONSISTENTE =====

    private (List<string> alternatives, List<int> correctAnswers) GetRandomizedAlternatives(
        GameRoom room, int round, List<string> original, List<int> originalCorrectAnswers)
    {
        var random = new Random(room.RoomCode.GetHashCode() + round);

        // Criar lista de tuplas (alternativa, índice original, é correta?)
        var indexedAlternatives = original
            .Select((alt, idx) => new {
                Text = alt,
                OriginalIndex = idx,
                IsCorrect = originalCorrectAnswers.Contains(idx)
            })
            .OrderBy(_ => random.Next())
            .ToList();

        // Separar alternativas embaralhadas
        var shuffled = indexedAlternatives.Select(x => x.Text).ToList();

        // Mapear novos índices das alternativas corretas
        var newCorrect = indexedAlternatives
            .Select((item, newIndex) => new { item.IsCorrect, newIndex })
            .Where(x => x.IsCorrect)
            .Select(x => x.newIndex)
            .ToList();

        return (shuffled, newCorrect);
    }

    // ===== VALIDAÇÕES E VERIFICAÇÕES =====

    public (bool canJoin, string message, bool isReturning, string? currentTeamId, string? currentTeamName)
        CheckTeamAccess(string roomCode, string teamId, string userId, UserManagementService userService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return (false, "Sala não encontrada", false, null, null);

        var team = room.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null)
            return (false, "Time não encontrado", false, null, null);

        bool gameStarted = room.GameStatus == "investigation" || room.GameStatus == "results";

        if (!gameStarted)
            return (true, "Você pode entrar neste time", false, null, null);

        var existingUser = userService.GetAllUsersInRoom(roomCode)
            .FirstOrDefault(u => u.Id == userId);

        if (existingUser != null && existingUser.TeamId == teamId)
            return (true, $"Bem-vindo de volta ao {team.Name}!", true, teamId, team.Name);

        if (existingUser != null)
        {
            var currentTeam = room.Teams.FirstOrDefault(t => t.Id == existingUser.TeamId);
            return (false, $"O jogo já iniciou! Você está no {currentTeam?.Name ?? "outro time"}.",
                    false, existingUser.TeamId, currentTeam?.Name);
        }

        return (false, "O jogo já iniciou! Não é possível entrar em novos times.", false, null, null);
    }

    public (bool isValid, string message) ValidateRoomAndRound(string roomCode, int round)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return (false, "Sala não encontrada");

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();

        if (round < 0 || round >= dossiers.Count)
            return (false, "Rodada inválida");

        if (room.UseCustomDossiers && !room.CustomDossiers.Any())
            return (false, "Esta sala usa perguntas personalizadas mas não possui nenhuma pergunta cadastrada");

        return (true, "Validação bem-sucedida");
    }

    // ===== SUBMISSÃO DE RESPOSTAS =====

    public (bool success, int score, bool isCorrect, string message, bool allTeamsResponded, bool advancedToNextRound)
        SubmitTeamAnswer(TeamAnswerRequestViewModel request, UserManagementService userService, SessionManagementService sessionService)
    {
        var room = RoomManager.GetRoom(request.RoomCode);
        if (room == null)
            return (false, 0, false, "Sala não encontrada", false, false);

        var (isValid, validationMessage) = ValidateRoomAndRound(request.RoomCode, request.Round);
        if (!isValid)
            return (false, 0, false, validationMessage, false, false);

        var team = room.Teams.FirstOrDefault(t => t.Id == request.TeamId);
        if (team?.Responses.ContainsKey(request.Round) is true)
            return (false, team.Responses[request.Round].Score, false, "Equipe já respondeu esta rodada", false, false);

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();
        var (shuffled, correctIndices) = GetRandomizedAlternatives(room, request.Round,
            dossiers[request.Round].Alternatives, dossiers[request.Round].CorrectAnswers);

        // ✅ Calcular pontuação individual e somar
        var (totalScore, individualScores, votingUsers) = votingService.CalculateTeamScore(
            request.RoomCode,
            request.TeamId,
            request.Round,
            correctIndices,
            userService
        );

        if (team is not null)
        {
            team.Score += totalScore;
            team.RoundScores.Add(totalScore);
            team.Responses[request.Round] = new()
            {
                SelectedAlternatives = new List<int>(),
                Timestamp = DateTime.Now,
                Score = totalScore,
                UserVotes = votingUsers,
                TotalUsers = userService.GetTeamUsers(request.RoomCode, request.TeamId).Count,
                CorrectAnswers = correctIndices,
                ShuffledAlternatives = shuffled
            };

            team.LastActivity = DateTime.Now;

            logger.LogInformation(
                $"Resposta registrada - Time: {team.Name}, Rodada: {request.Round + 1}, " +
                $"Jogadores que votaram: {votingUsers.Count}, Pontuação total: {totalScore}"
            );
        }

        var message = FormatScoreMessage(totalScore);
        var (allResponded, advanced) = CheckAndAdvanceRound(room, request.Round, dossiers.Count, sessionService);

        return (true, totalScore, totalScore >= 10, message, allResponded, advanced);
    }

    // ===== GERENCIAMENTO DE RODADAS =====

    public (bool success, string message) ResetCurrentRound(string roomCode, UserManagementService userService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return (false, "Sala não encontrada");

        try
        {
            foreach (var team in room.Teams)
            {
                if (team.Responses.ContainsKey(room.CurrentRound))
                {
                    var roundScore = team.Responses[room.CurrentRound].Score;
                    team.Score -= roundScore;
                    team.Responses.Remove(room.CurrentRound);

                    if (team.RoundScores.Count > 0)
                        team.RoundScores.RemoveAt(team.RoundScores.Count - 1);
                }
            }

            // ✅ Limpa apenas os votos da rodada atual (para resetar)
            votingService.ClearRoundVotes(roomCode, room.CurrentRound);

            var allUsers = userService.GetAllUsersInRoom(roomCode);
            foreach (var user in allUsers)
            {
                user.HasVoted = false;
                user.CurrentVotes = new List<int>();
            }

            room.GameStatus = "investigation";
            room.LastResetTime = DateTime.Now;

            logger.LogInformation("Rodada {Round} resetada na sala {RoomCode}", room.CurrentRound, roomCode);

            return (true, $"Rodada {room.CurrentRound + 1} resetada com sucesso!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao resetar rodada");
            return (false, "Erro ao resetar rodada");
        }
    }

    private (bool allResponded, bool advanced) CheckAndAdvanceRound(
        GameRoom room, int round, int totalRounds, SessionManagementService sessionService)
    {
        var onlineTeamIds = sessionService.GetConnectedTeams(room.RoomCode);
        var onlineTeams = room.Teams.Where(t => onlineTeamIds.Contains(t.Id)).ToList();
        var allResponded = onlineTeams.Any() && onlineTeams.All(t => t.Responses.ContainsKey(round));

        if (allResponded && room.GameStatus == "investigation")
            return (true, AdvanceToNextRound(room, totalRounds));

        return (allResponded, false);
    }

    private bool AdvanceToNextRound(GameRoom room, int totalRounds)
    {
        if (room.CurrentRound < totalRounds - 1)
        {
            room.CurrentRound++;
            room.GameStatus = room.CurrentRound == 0 ? "presentation" : "investigation";

            logger.LogInformation("Sala {RoomCode} avançou para rodada {Round}", room.RoomCode, room.CurrentRound + 1);
            return true;
        }

        room.GameStatus = "finished";
        logger.LogInformation("Sala {RoomCode} finalizou o jogo", room.RoomCode);
        return false;
    }

    // ===== GERENCIAMENTO DE DOSSIERS =====

    public object GetDossier(string roomCode, int round)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room is null)
            return new { success = false, message = "Sala não encontrada" };

        var (isValid, validationMessage) = ValidateRoomAndRound(roomCode, round);
        if (!isValid)
            return new { success = false, message = validationMessage };

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();
        var dossier = dossiers[round];
        var (alternatives, correctAnswers) = GetRandomizedAlternatives(room, round,
            dossier.Alternatives, dossier.CorrectAnswers);

        return new
        {
            success = true,
            dossier = new
            {
                dossier.Title,
                dossier.Name,
                dossier.Description,
                dossier.Challenge,
                dossier.Objective,
                Alternatives = alternatives,
                Explanation = "",
                CorrectAnswers = correctAnswers
            },
            currentRound = round,
            totalRounds = dossiers.Count,
            gameStatus = room.GameStatus
        };
    }

    // ===== CONTROLES DE FASE DO JOGO =====

    public (bool success, string message, string gameStatus, int connectedTeams) StartInvestigation(
        string roomCode, SessionManagementService sessionService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room is null)
            return (false, "Sala não encontrada", "error", 0);

        var connectedTeams = sessionService.GetConnectedTeamsCount(roomCode);
        if (connectedTeams < 2)
            return (false, $"É necessário ter pelo menos 2 equipes conectadas. Atualmente há {connectedTeams}.", "setup", connectedTeams);

        room.GameStatus = "investigation";
        logger.LogInformation("Investigação iniciada na sala {RoomCode} com {Teams} equipes", roomCode, connectedTeams);

        return (true, $"Fase de investigação iniciada com {connectedTeams} equipes!", room.GameStatus, connectedTeams);
    }

    public (bool success, string message, string gameStatus, List<int> correctAnswers, string explanation) ShowResults(string roomCode)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room is null)
            return (false, "Sala não encontrada", "error", [], "");

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();
        if (room.CurrentRound < 0 || room.CurrentRound >= dossiers.Count)
            return (false, "Rodada inválida", room.GameStatus, [], "");

        room.GameStatus = "results";
        var dossier = dossiers[room.CurrentRound];
        var (_, correctIndices) = GetRandomizedAlternatives(room, room.CurrentRound,
            dossier.Alternatives, dossier.CorrectAnswers);

        logger.LogInformation("Resultados exibidos na sala {RoomCode}, rodada {Round}", roomCode, room.CurrentRound + 1);

        return (true, "Resultados exibidos", room.GameStatus, correctIndices, dossier.Explanation ?? "");
    }

    public (bool success, string message, bool shouldReload, bool finished) NextRound(
        string roomCode, SessionManagementService sessionService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room is null)
            return (false, "Sala não encontrada", false, false);

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();

        if (room.CurrentRound >= dossiers.Count - 1)
        {
            room.GameStatus = "finished";
            logger.LogInformation("Jogo finalizado na sala {RoomCode}", roomCode);
            return (true, "Jogo finalizado!", true, true);
        }

        room.CurrentRound++;
        room.GameStatus = "investigation";

        logger.LogInformation("Sala {RoomCode} avançou para rodada {Round}", roomCode, room.CurrentRound + 1);

        return (true, $"Avançando para rodada {room.CurrentRound + 1}!", true, false);
    }

    // ===== RESULTADOS E ESTATÍSTICAS =====

    public object GetGameState(string roomCode, SessionManagementService sessionService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return new { success = false, message = "Sala não encontrada" };

        var connectedTeamIds = sessionService.GetConnectedTeams(roomCode);
        var onlineTeams = room.Teams.Where(t => connectedTeamIds.Contains(t.Id)).ToList();
        bool allTeamsResponded = onlineTeams.Any() && onlineTeams.All(t => t.Responses.ContainsKey(room.CurrentRound));

        var activeTeams = room.Teams.Select(t => new
        {
            t.Id,
            t.Name,
            t.Score,
            t.RoundScores,
            ResponseCount = t.Responses.ContainsKey(room.CurrentRound) ? 1 : 0,
            IsActive = sessionService.IsTeamActive(roomCode, t.Id),
            IsConnected = connectedTeamIds.Contains(t.Id),
            t.Icon,
            HasSubmitted = t.Responses.ContainsKey(room.CurrentRound)
        }).ToList();

        return new
        {
            success = true,
            gameStatus = room.GameStatus,
            currentRound = room.CurrentRound,
            connectedTeams = connectedTeamIds.Count,
            teams = activeTeams,
            allTeamsResponded,
            resetTime = room.LastResetTime
        };
    }

    public object GetFinalResults(string roomCode, SessionManagementService sessionService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return new { success = false, message = "Sala não encontrada" };

        var sortedTeams = room.Teams
            .OrderByDescending(t => t.Score)
            .Select((t, index) => new
            {
                Position = index + 1,
                t.Id,
                t.Name,
                t.Score,
                t.RoundScores,
                IsActive = sessionService.IsTeamActive(roomCode, t.Id)
            }).ToList();

        return new
        {
            success = true,
            teams = sortedTeams,
            totalRounds = room.CurrentRound + 1,
            totalConnectedTeams = sessionService.GetConnectedTeamsCount(roomCode)
        };
    }

    public object GetTeamDetailedResults(string roomCode, string teamId, UserManagementService userService)
    {
        var room = RoomManager.GetRoom(roomCode);
        if (room == null)
            return new { success = false, message = "Sala não encontrada" };

        var team = room.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null)
            return new { success = false, message = "Equipe não encontrada" };

        var dossiers = room.UseCustomDossiers ? room.CustomDossiers : GetGameDossiers();
        var detailedResults = new List<object>();

        for (int round = 0; round < dossiers.Count; round++)
        {
            var dossier = dossiers[round];
            var hasResponse = team.Responses.ContainsKey(round);

            if (hasResponse)
            {
                var response = team.Responses[round];

                var (shuffledAlternatives, correctIndices) = GetRandomizedAlternatives(
                    room,
                    round,
                    dossier.Alternatives,
                    dossier.CorrectAnswers
                );

                var teamVotes = votingService.GetTeamVotes(roomCode, teamId, round, userService);

                var votesByAlternative = new Dictionary<int, int>();
                foreach (var userVote in teamVotes.Values)
                {
                    foreach (var vote in userVote)
                    {
                        if (!votesByAlternative.ContainsKey(vote))
                            votesByAlternative[vote] = 0;
                        votesByAlternative[vote]++;
                    }
                }

                var answersAnalysis = shuffledAlternatives.Select((alt, index) => {
                    bool isCorrect = correctIndices.Contains(index);
                    bool wasSelected = votesByAlternative.ContainsKey(index);
                    int voteCount = wasSelected ? votesByAlternative[index] : 0;

                    string status;
                    if (wasSelected && isCorrect)
                        status = "correct";
                    else if (wasSelected && !isCorrect)
                        status = "wrong";
                    else if (!wasSelected && isCorrect)
                        status = "missed";
                    else
                        status = "not-selected";

                    return new
                    {
                        Index = index,
                        Text = alt,
                        IsCorrect = isCorrect,
                        WasSelected = wasSelected,
                        VoteCount = voteCount,
                        Status = status
                    };
                }).ToList();

                detailedResults.Add(new
                {
                    Round = round + 1,
                    DossierTitle = dossier.Title,
                    DossierName = dossier.Name,
                    Score = response.Score,
                    MaxScore = response.TotalUsers * 20,
                    SelectedAlternatives = votesByAlternative.Keys.ToList(),
                    CorrectAlternatives = correctIndices,
                    Explanation = dossier.Explanation,
                    AnswersAnalysis = answersAnalysis,
                    IsFullyCorrect = response.Score == (response.TotalUsers * 20),
                    IsPartiallyCorrect = response.Score > 0 && response.Score < (response.TotalUsers * 20),
                    IsIncorrect = response.Score == 0,
                    TotalVoters = response.TotalUsers
                });
            }
            else
            {
                var (shuffledAlternatives, correctIndices) = GetRandomizedAlternatives(
                    room,
                    round,
                    dossier.Alternatives,
                    dossier.CorrectAnswers
                );

                var answersAnalysis = shuffledAlternatives.Select((alt, index) => new {
                    Index = index,
                    Text = alt,
                    IsCorrect = correctIndices.Contains(index),
                    WasSelected = false,
                    VoteCount = 0,
                    Status = "not-selected"
                }).ToList();

                detailedResults.Add(new
                {
                    Round = round + 1,
                    DossierTitle = dossier.Title,
                    DossierName = dossier.Name,
                    Score = 0,
                    MaxScore = 0,
                    SelectedAlternatives = new List<int>(),
                    CorrectAlternatives = correctIndices,
                    Explanation = dossier.Explanation,
                    AnswersAnalysis = answersAnalysis,
                    IsFullyCorrect = false,
                    IsPartiallyCorrect = false,
                    IsIncorrect = false,
                    TotalVoters = 0,
                    NotAnswered = true
                });
            }
        }

        int totalMaxPossibleScore = detailedResults
            .Cast<dynamic>()
            .Sum(r => (int)r.MaxScore);

        return new
        {
            success = true,
            teamName = team.Name,
            teamIcon = team.Icon,
            totalScore = team.Score,
            maxPossibleScore = totalMaxPossibleScore,
            detailedResults,
            totalRounds = dossiers.Count,
            answeredRounds = team.Responses.Count
        };
    }

    public object GetTeamVotingStatus(string roomCode, string teamId, UserManagementService userService)
    {
        var teamUsers = userService.GetTeamUsers(roomCode, teamId)
            .Select(u => new
            {
                Id = u.Id ?? string.Empty,
                Name = u.Name ?? "Jogador",
                u.HasVoted,
                u.IsLeader,
                CurrentVotes = u.CurrentVotes ?? new List<int>(),
                u.LastActivity,
                u.MissedVotes
            })
            .Where(u => !string.IsNullOrEmpty(u.Id))
            .ToList();

        var totalUsers = teamUsers.Count;
        var votedUsers = teamUsers.Count(u => u.HasVoted);
        var votingPercentage = totalUsers > 0 ? (votedUsers * 100) / totalUsers : 0;

        var allVotes = teamUsers
            .Where(u => u.HasVoted && u.CurrentVotes != null)
            .SelectMany(u => u.CurrentVotes)
            .GroupBy(v => v)
            .Select(g => new {
                Alternative = g.Key,
                Votes = g.Count(),
                Percentage = totalUsers > 0 ? (g.Count() * 100) / totalUsers : 0
            })
            .OrderByDescending(v => v.Votes)
            .ToList();

        return new
        {
            success = true,
            users = teamUsers,
            votingSummary = new
            {
                TotalUsers = totalUsers,
                VotedUsers = votedUsers,
                VotingPercentage = votingPercentage,
                VoteDistribution = allVotes
            }
        };
    }

    // ===== MÉTODOS AUXILIARES =====
    private static string FormatScoreMessage(int score) => score switch
    {
        >= 40 => "🎉 Excelente! Time perfeito! +" + score + " pontos",
        >= 20 => "✅ Muito bom! +" + score + " pontos",
        >= 10 => "👍 Bom trabalho! +" + score + " pontos",
        _ => "❌ Nenhum jogador acertou. Tentem na próxima!"
    };

    // ===== DOSSIERS PADRÃO - AGORA COM ÍNDICES CORRETOS FIXOS =====
    public List<Dossier> GetGameDossiers() =>
    [
        new()
        {
            Title = "Maria, a Artesã Empreendedora",
            Name = "Maria Silva",
            Description = "Maria vende suas artes apenas em feiras locais e deseja alcançar clientes de outras cidades e estados. Muitos clientes pedem nota fiscal, mas ela não é formalizada e ainda não vende online.",
            Challenge = "As vendas estão limitadas ao alcance local e ela perde clientes por não emitir nota fiscal.",
            Objective = "Formalizar o negócio, se tornar MEI, vender online e organizar melhor suas finanças.",
            Alternatives = [
                "Consultoria – Economia Criativa RR",           // 0 - CORRETA
                "Comece a ser MEI",                             // 1 - CORRETA
                "Comece a vender mais através do Marketing Digital", // 2 - CORRETA
                "Comece o seu Plano de Negócio",                // 3
                "BUSINESS MODEL CANVAS: VALIDE SEU MODELO DE NEGÓCIO", // 4
                "Palestra Pré-Startup Mundi"                    // 5
            ],
            CorrectAnswers = [0, 1, 2], // ✅ Índices fixos das corretas
        },
        new()
        {
            Title = "João, o Marceneiro que Precisa de Certificação",
            Name = "João Pereira",
            Description = "João recebeu a proposta de fornecer móveis para uma grande rede de hotéis, mas precisa da certificação ISO 9001.",
            Challenge = "Sem a certificação ISO 9001 ele não poderá fechar o contrato.",
            Objective = "Obter a certificação ISO 9001 e melhorar seus processos internos.",
            Alternatives = [
                "Faça sua Gestão Estratégica de Negócio",       // 0 - CORRETA
                "Consultoria Estruturada em Organização",       // 1 - CORRETA
                "Aprenda a Gerir Compras e Estoque",            // 2 - CORRETA
                "Missão Técnica",                               // 3
                "Consultoria em Gestão Financeira",             // 4
                "4 pontos essenciais na sua estratégia de marketing digital" // 5
            ],
            CorrectAnswers = [0, 1, 2],
        },
        new()
        {
            Title = "Ana, a Produtora Rural Orgânica",
            Name = "Ana Costa",
            Description = "Ana foi procurada por uma grande rede de supermercados interessada em seus produtos, mas precisa da certificação oficial de orgânico.",
            Challenge = "Sem a certificação, ela não pode fechar contrato com a rede.",
            Objective = "Obter certificação orgânica e expandir seu mercado.",
            Alternatives = [
                "Alcance a Consultoria em Agricultura Familiar", // 0 - CORRETA
                "Comece a desenvolver processo de incubação no Modelo CERNE", // 1 - CORRETA
                "Comece o seu Projeto de Avicultura",           // 2 - CORRETA
                "Atendimento ao cliente",                       // 3
                "Diagnóstico empresarial",                      // 4
                "Oficina Canvas - Modelagem de Negócios"        // 5
            ],
            CorrectAnswers = [0, 1, 2],
        },
        new()
        {
            Title = "Carlos, o Desenvolvedor de Software em Crescimento",
            Name = "Carlos Mendes",
            Description = "A empresa de Carlos foi convidada para uma licitação que exige certificação MPS.BR.",
            Challenge = "Sem o MPS.BR ele não pode participar da licitação.",
            Objective = "Adequar processos internos e obter a certificação MPS.BR.",
            Alternatives = [
                "CONSULTORIA DE ORIENTAÇÃO - PLANEJAMENTO",     // 0 - CORRETA
                "Empreendedorismo e Inovação",                  // 1 - CORRETA
                "Hackathon",                                    // 2 - CORRETA
                "Destaque-se em seu Ambiente de Trabalho",      // 3
                "Diagnóstico empresarial",                      // 4
                "Oficina de Internacionalização – Missão Empresarial" // 5
            ],
            CorrectAnswers = [0, 1, 2],
        },
        new()
        {
            Title = "Beatriz, a Empreendedora Digital sem Identidade",
            Name = "Beatriz Oliveira",
            Description = "Beatriz quer desenvolver uma identidade visual forte para seus produtos naturais.",
            Challenge = "Suas embalagens e marca são genéricas e não se destacam.",
            Objective = "Criar identidade visual profissional, reforçar posicionamento e facilitar venda em lojas.",
            Alternatives = [
                "Marketing Digital para Pequenos Negócios",     // 0 - CORRETA
                "Registro de Marca",                            // 1 - CORRETA
                "Consultoria em Gestão - Marketing Digital",    // 2 - CORRETA
                "Comece o seu Plano de Negócio",                // 3
                "Comece a vender mais através do Marketing DigitalConsultoria", // 4
                "Faça sua Modelagem de Negócios com CANVAS"     // 5
            ],
            CorrectAnswers = [0, 1, 2],
        },
        new()
        {
            Title = "Ricardo, o Restaurante que Precisa de Eficiência",
            Name = "Ricardo Santos",
            Description = "Seu restaurante sofre com desperdícios, estoque desorganizado e falta de controle financeiro.",
            Challenge = "A desorganização está reduzindo a lucratividade.",
            Objective = "Organizar estoque, reduzir desperdício e profissionalizar a operação.",
            Alternatives = [
                "Consultoria Flexível - Alimentação Fora do Lar e Indústria de Alimentos e Bebida", // 0 - CORRETA
                "Aprenda a Gerir Compras e Estoque",            // 1 - CORRETA
                "Consultoria em Gestão - Indicadores de Desempenho Econômico-financeiros", // 2 - CORRETA
                "Oficina Canvas - Modelagem de Negócios",       // 3
                "Consultoria em Gestão - Gestão de Compras e Estoques", // 4
                "Consultoria em Gestão - Gestão de riscos"      // 5
            ],
            CorrectAnswers = [0, 1, 2],
        }
    ];
}