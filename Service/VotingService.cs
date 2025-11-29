using ApplicationSebrae.Models;
using ApplicationSebrae.ViewModels;
using Microsoft.Extensions.Logging;

namespace ApplicationSebrae.Services
{
    public class VotingService
    {
        private static Dictionary<string, Dictionary<int, Dictionary<string, List<int>>>> _userVotes = new Dictionary<string, Dictionary<int, Dictionary<string, List<int>>>>();
        private static readonly object _voteLock = new object();
        private readonly ILogger<VotingService> _logger;

        public VotingService(ILogger<VotingService> logger)
        {
            _logger = logger;
        }

        public (bool success, string message, bool hasVoted, int voteCount) SubmitVote(VoteSubmissionViewModel submission, UserManagementService userService)
        {
            var room = RoomManager.GetRoom(submission.RoomCode);
            if (room == null)
            {
                return (false, "Sala não encontrada", false, 0);
            }

            // Validação: Verificar se o jogo está em fase de investigação
            if (room.GameStatus != "investigation")
            {
                string statusMessage = room.GameStatus switch
                {
                    "setup" => "Aguarde o facilitador iniciar a investigação",
                    "presentation" => "Aguarde a apresentação do caso terminar",
                    "results" => "Votação encerrada - aguarde a próxima rodada",
                    "finished" => "O jogo foi finalizado",
                    _ => "Não é possível votar neste momento"
                };

                _logger.LogWarning($"Tentativa de voto bloqueada - Status: {room.GameStatus}, User: {submission.UserId}");
                return (false, statusMessage, false, 0);
            }

            lock (_voteLock)
            {
                var users = userService.GetAllUsersInRoom(submission.RoomCode);
                var user = users.FirstOrDefault(u => u.Id == submission.UserId);
                if (user == null)
                {
                    return (false, "Usuário não encontrado", false, 0);
                }

                // Validação: deve selecionar exatamente 2 alternativas
                if (submission.SelectedAlternatives.Count != 2)
                {
                    return (false, "Você deve selecionar exatamente 2 alternativas", false, 0);
                }

                // Validação: alternativas devem ser únicas
                if (submission.SelectedAlternatives.Distinct().Count() != 2)
                {
                    return (false, "Selecione 2 alternativas diferentes", false, 0);
                }

                // ✅ ATUALIZADO: Validação dinâmica do range baseada no número de alternativas do dossier atual
                int maxAlternativeIndex = 5; // Default

                if (room.CurrentRound >= 0)
                {
                    if (room.UseCustomDossiers && room.CurrentRound < room.CustomDossiers.Count)
                    {
                        // Perguntas personalizadas: obtém número real de alternativas
                        var currentDossier = room.CustomDossiers[room.CurrentRound];
                        maxAlternativeIndex = currentDossier.Alternatives.Count - 1;
                    }
                    else if (!room.UseCustomDossiers)
                    {
                        // Perguntas fixas: sempre 6 alternativas (0-5)
                        maxAlternativeIndex = 5;
                    }
                }

                if (submission.SelectedAlternatives.Any(a => a < 0 || a > maxAlternativeIndex))
                {
                    return (false, "Alternativas inválidas", false, 0);
                }

                // Atualizar estado do usuário
                user.CurrentVotes = submission.SelectedAlternatives;
                user.HasVoted = true;
                user.LastVoteTime = DateTime.Now;
                user.LastActivity = DateTime.Now;

                if (user.HasVoted)
                {
                    user.MissedVotes = 0;
                }

                // Salvar voto no dicionário principal
                if (!_userVotes.ContainsKey(submission.RoomCode))
                {
                    _userVotes[submission.RoomCode] = new Dictionary<int, Dictionary<string, List<int>>>();
                }

                if (!_userVotes[submission.RoomCode].ContainsKey(room.CurrentRound))
                {
                    _userVotes[submission.RoomCode][room.CurrentRound] = new Dictionary<string, List<int>>();
                }

                _userVotes[submission.RoomCode][room.CurrentRound][submission.UserId] = submission.SelectedAlternatives;

                _logger.LogInformation($"Voto registrado - Usuário: {user.Name}, Time: {user.TeamId}, Rodada: {room.CurrentRound}, Votos: {string.Join(", ", submission.SelectedAlternatives)}");

                return (true, "Voto registrado com sucesso", user.HasVoted, submission.SelectedAlternatives.Count);
            }
        }

        // ✅ ATUALIZADO: Agora suporta qualquer número de respostas corretas
        public int CalculateScore(List<int> selectedAlternatives, List<int> correctAnswers)
        {
            if (selectedAlternatives == null || selectedAlternatives.Count == 0)
            {
                return 0;
            }

            // Conta quantas das alternativas selecionadas estão corretas
            int correctCount = selectedAlternatives.Count(correctAnswers.Contains);

            // ✅ LÓGICA ATUALIZADA: Proporcional ao número de respostas corretas
            // Cada acerto vale 10 pontos
            // Máximo: 20 pontos (acertar 2 das corretas, já que cada jogador vota em 2)
            int score = correctCount * 10;

            _logger.LogDebug(
                $"Cálculo de pontuação - Selecionadas: [{string.Join(", ", selectedAlternatives)}], " +
                $"Corretas: [{string.Join(", ", correctAnswers)}], " +
                $"Acertos: {correctCount}, Pontos: {score}"
            );

            return score;
        }

        // ✅ ATUALIZADO: Documentação melhorada
        /// <summary>
        /// Calcula a pontuação total do time somando as pontuações individuais de cada jogador
        /// </summary>
        /// <param name="correctAnswers">Lista dos índices corretos JÁ EMBARALHADOS para esta rodada</param>
        public (int totalScore, Dictionary<string, int> individualScores, List<string> votingUsers) CalculateTeamScore(
            string roomCode,
            string teamId,
            int round,
            List<int> correctAnswers,
            UserManagementService userService)
        {
            var teamVotes = GetTeamVotes(roomCode, teamId, round, userService);
            var individualScores = new Dictionary<string, int>();
            int totalScore = 0;
            var votingUsers = new List<string>();

            foreach (var userVote in teamVotes)
            {
                string userId = userVote.Key;
                List<int> userAlternatives = userVote.Value;

                int userScore = CalculateScore(userAlternatives, correctAnswers);
                individualScores[userId] = userScore;
                totalScore += userScore;
                votingUsers.Add(userId);

                var user = userService.GetAllUsersInRoom(roomCode).FirstOrDefault(u => u.Id == userId);
                _logger.LogInformation(
                    $"Pontuação individual - Usuário: {user?.Name ?? userId}, Time: {teamId}, Rodada: {round}, " +
                    $"Votos: [{string.Join(", ", userAlternatives)}], Corretas: [{string.Join(", ", correctAnswers)}], " +
                    $"Acertos: {userAlternatives.Count(correctAnswers.Contains)}, Pontos: {userScore}"
                );
            }

            _logger.LogInformation(
                $"Pontuação total do time - Time: {teamId}, Rodada: {round}, " +
                $"Jogadores que votaram: {votingUsers.Count}, Pontuação total: {totalScore}"
            );

            return (totalScore, individualScores, votingUsers);
        }

        public Dictionary<string, List<int>> GetTeamVotes(string roomCode, string teamId, int round, UserManagementService userService)
        {
            lock (_voteLock)
            {
                var votes = new Dictionary<string, List<int>>();

                // ✅ DEBUG: Verificar estrutura de dados
                _logger.LogDebug(
                    $"GetTeamVotes - RoomCode: {roomCode}, TeamId: {teamId}, Round: {round}, " +
                    $"RoomExists: {_userVotes.ContainsKey(roomCode)}, " +
                    $"RoundExists: {_userVotes.ContainsKey(roomCode) && _userVotes[roomCode].ContainsKey(round)}"
                );

                if (_userVotes.ContainsKey(roomCode) && _userVotes[roomCode].ContainsKey(round))
                {
                    var teamUsers = userService.GetAllUsersInRoom(roomCode).Where(u => u.TeamId == teamId);

                    foreach (var user in teamUsers)
                    {
                        if (_userVotes[roomCode][round].ContainsKey(user.Id))
                        {
                            votes[user.Id] = _userVotes[roomCode][round][user.Id];
                            _logger.LogDebug(
                                $"Voto encontrado - User: {user.Id}, Votos: [{string.Join(", ", votes[user.Id])}]"
                            );
                        }
                    }
                }
                else
                {
                    _logger.LogWarning(
                        $"GetTeamVotes - Nenhum voto encontrado para Room: {roomCode}, Round: {round}"
                    );
                }

                return votes;
            }
        }

        public Dictionary<int, int> GetVoteDistribution(string roomCode, string teamId, int round, UserManagementService userService)
        {
            var teamVotes = GetTeamVotes(roomCode, teamId, round, userService);
            var distribution = new Dictionary<int, int>();

            foreach (var userVotes in teamVotes.Values)
            {
                foreach (var vote in userVotes)
                {
                    if (!distribution.ContainsKey(vote))
                    {
                        distribution[vote] = 0;
                    }
                    distribution[vote]++;
                }
            }

            return distribution;
        }

        public void ClearRoundVotes(string roomCode, int round)
        {
            lock (_voteLock)
            {
                // ⚠️ CORREÇÃO: Só limpa se round for >= 0
                // round = -1 significa "limpar tudo"
                if (round < 0)
                {
                    // Limpar todos os votos da sala
                    if (_userVotes.ContainsKey(roomCode))
                    {
                        _userVotes.Remove(roomCode);
                        _logger.LogInformation($"Todos os votos da sala {roomCode} foram limpos");
                    }
                }
                else if (_userVotes.ContainsKey(roomCode) && _userVotes[roomCode].ContainsKey(round))
                {
                    _userVotes[roomCode].Remove(round);
                    _logger.LogInformation($"Votos da rodada {round} limpos na sala {roomCode}");
                }
            }
        }

        public void ClearAllVotes()
        {
            lock (_voteLock)
            {
                _userVotes.Clear();
                _logger.LogInformation("Todos os votos foram limpos do sistema");
            }
        }

        public (bool allVoted, int votedCount, int totalCount) GetTeamVotingStatus(string roomCode, string teamId, UserManagementService userService)
        {
            var teamUsers = userService.GetAllUsersInRoom(roomCode).Where(u => u.TeamId == teamId).ToList();
            var votedCount = teamUsers.Count(u => u.HasVoted);

            return (votedCount == teamUsers.Count && teamUsers.Count > 0, votedCount, teamUsers.Count);
        }

        public object GetTeamVotingStatistics(string roomCode, string teamId, int round, UserManagementService userService)
        {
            var distribution = GetVoteDistribution(roomCode, teamId, round, userService);
            var teamVotes = GetTeamVotes(roomCode, teamId, round, userService);
            var votingStatus = GetTeamVotingStatus(roomCode, teamId, userService);

            return new
            {
                Distribution = distribution,
                TotalVotes = teamVotes.Count,
                AllVoted = votingStatus.allVoted,
                VotedCount = votingStatus.votedCount,
                TotalUsers = votingStatus.totalCount,
                VotingPercentage = votingStatus.totalCount > 0 ? (double)votingStatus.votedCount / votingStatus.totalCount : 0
            };
        }
    }
}