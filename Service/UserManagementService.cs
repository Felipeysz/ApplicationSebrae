using ApplicationSebrae.Models;
using ApplicationSebrae.ViewModels;
using Microsoft.Extensions.Logging;

namespace ApplicationSebrae.Services
{
    public class UserManagementService
    {
        private static Dictionary<string, Dictionary<string, TeamUser>> _teamUsers = new Dictionary<string, Dictionary<string, TeamUser>>();
        private static readonly object _userLock = new object();
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(ILogger<UserManagementService> logger)
        {
            _logger = logger;
        }

        // ✅ MÉTODO UNIFICADO: JoinTeam com validação de bloqueio e sistema de IDs
        public (bool success, string userId, bool isNewUser, bool isLeader, string message) JoinTeam(JoinTeamViewModel model)
        {
            try
            {
                _logger.LogInformation($"JoinTeam chamado - Room: {model.RoomCode}, Team: {model.TeamId}, UserId: {model.UserId}, UserName: {model.UserName}");

                if (string.IsNullOrEmpty(model.RoomCode) || string.IsNullOrEmpty(model.TeamId))
                {
                    _logger.LogWarning("JoinTeam: Dados inválidos - RoomCode ou TeamId vazio");
                    return (false, string.Empty, false, false, "Dados inválidos para entrar no time.");
                }

                if (string.IsNullOrEmpty(model.UserName) || model.UserName.Trim().Length < 2)
                {
                    _logger.LogWarning("JoinTeam: Nome inválido ou muito curto");
                    return (false, string.Empty, false, false, "Nome é obrigatório e deve ter pelo menos 2 caracteres.");
                }

                var room = RoomManager.GetRoom(model.RoomCode);
                if (room == null)
                {
                    _logger.LogWarning($"JoinTeam: Sala não encontrada - {model.RoomCode}");
                    return (false, string.Empty, false, false, "Sala não encontrada");
                }

                var team = room.Teams.FirstOrDefault(t => t.Id == model.TeamId);
                if (team == null)
                {
                    _logger.LogWarning($"JoinTeam: Equipe não encontrada - {model.TeamId}");
                    return (false, string.Empty, false, false, "Equipe não encontrada");
                }

                // ✅ VALIDAÇÃO: Se jogo iniciou, verificar restrições
                if (room.GameStatus == "investigation" || room.GameStatus == "results")
                {
                    _logger.LogInformation($"JoinTeam: Jogo em andamento - Status: {room.GameStatus}");

                    // Verificar se usuário já existe na sala
                    var existingUser = GetAllUsersInRoom(model.RoomCode)
                        .FirstOrDefault(u => u.Id == model.UserId);

                    if (existingUser != null)
                    {
                        // Usuário existe - verificar se está tentando trocar de time
                        if (existingUser.TeamId != model.TeamId)
                        {
                            _logger.LogWarning($"Usuário {existingUser.Name} tentou trocar do time {existingUser.TeamId} para {model.TeamId} durante o jogo");
                            return (false, string.Empty, false, false,
                                $"O jogo já iniciou! Você não pode trocar de time. Volte para o {room.Teams.FirstOrDefault(t => t.Id == existingUser.TeamId)?.Name ?? "seu time"}.");
                        }

                        // Usuário está retornando ao próprio time - permitir
                        lock (_userLock)
                        {
                            if (_teamUsers.ContainsKey(model.RoomCode) && _teamUsers[model.RoomCode].ContainsKey(existingUser.Id))
                            {
                                var user = _teamUsers[model.RoomCode][existingUser.Id];
                                user.IsConnected = true;
                                user.LastActivity = DateTime.Now;
                                user.Name = model.UserName.Trim();
                            }
                        }

                        _logger.LogInformation($"Usuário {existingUser.Name} retornou ao time {model.TeamId}");

                        return (true, existingUser.Id ?? string.Empty, false, existingUser.IsLeader,
                            $"Bem-vindo de volta, {existingUser.Name}!");
                    }
                    else
                    {
                        // Novo usuário tentando entrar após o jogo iniciar
                        _logger.LogWarning($"Novo usuário tentou entrar no time {model.TeamId} após o jogo iniciar");
                        return (false, string.Empty, false, false,
                            "O jogo já iniciou! Não é possível entrar em novos times no momento.");
                    }
                }

                // ✅ Jogo não iniciou - permitir entrada normal com sistema de IDs
                lock (_userLock)
                {
                    if (!_teamUsers.ContainsKey(model.RoomCode))
                    {
                        _logger.LogInformation($"JoinTeam: Criando novo dicionário para sala {model.RoomCode}");
                        _teamUsers[model.RoomCode] = new Dictionary<string, TeamUser>();
                    }

                    var teamUsers = _teamUsers[model.RoomCode].Where(u => u.Value.TeamId == model.TeamId).ToList();

                    // Verificar limite de jogadores por time
                    if (teamUsers.Count >= 5)
                    {
                        _logger.LogWarning($"JoinTeam: Equipe cheia - {model.TeamId} já tem {teamUsers.Count} usuários");
                        return (false, string.Empty, false, false, "Equipe cheia (máximo 5 jogadores)");
                    }

                    string userId;
                    bool isNewUser = false;
                    bool isLeader = false;

                    // Verificar se usuário já existe (por ID ou nome)
                    TeamUser? existingUser = null;

                    if (!string.IsNullOrEmpty(model.UserId) && model.UserId != "undefined" && model.UserId != "null")
                    {
                        // Buscar por ID
                        if (_teamUsers[model.RoomCode].ContainsKey(model.UserId))
                        {
                            existingUser = _teamUsers[model.RoomCode][model.UserId];
                        }
                    }

                    if (existingUser == null && !string.IsNullOrEmpty(model.UserName))
                    {
                        // Buscar por nome (case insensitive)
                        existingUser = _teamUsers[model.RoomCode].Values
                            .FirstOrDefault(u => u.Name?.Equals(model.UserName.Trim(), StringComparison.OrdinalIgnoreCase) == true);
                    }

                    if (existingUser != null)
                    {
                        // Usuário existente retornando
                        userId = existingUser.Id;
                        existingUser.TeamId = model.TeamId;
                        existingUser.IsConnected = true;
                        existingUser.LastActivity = DateTime.Now;
                        existingUser.Name = model.UserName.Trim();

                        _logger.LogInformation($"JoinTeam: Usuário existente reconectado - {existingUser.Name} (ID: {existingUser.Id})");

                        return (true, userId, false, existingUser.IsLeader,
                            $"Bem-vindo de volta, {existingUser.Name}!");
                    }
                    else
                    {
                        // Criar novo usuário
                        _logger.LogInformation("JoinTeam: Criando novo usuário");

                        userId = GenerateUserId();
                        isNewUser = true;
                        isLeader = teamUsers.Count == 0;

                        var newUser = new TeamUser
                        {
                            Id = userId,
                            Name = model.UserName.Trim(),
                            TeamId = model.TeamId,
                            IsConnected = true,
                            LastActivity = DateTime.Now,
                            JoinTime = DateTime.Now,
                            HasVoted = false,
                            MissedVotes = 0,
                            IsLeader = isLeader,
                            CurrentVotes = new List<int>(),
                            JoinedAt = DateTime.Now
                        };

                        _teamUsers[model.RoomCode][userId] = newUser;

                        _logger.LogInformation($"Novo usuário registrado: {newUser.Name} (ID: {newUser.Id}) no time {model.TeamId} - Líder: {isLeader}");
                        _logger.LogInformation($"Total de usuários na sala {model.RoomCode} após join: {_teamUsers[model.RoomCode].Count}");

                        return (true, userId, isNewUser, isLeader,
                            isLeader ? "Você é o líder da equipe!" : "Bem-vindo ao time!");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no JoinTeam");
                return (false, string.Empty, false, false, "Erro interno ao entrar no time");
            }
        }

        // ✅ MÉTODO: Gerar ID único para usuário
        public string GenerateUserId()
        {
            Random random = new Random();
            int attempts = 0;

            lock (_userLock)
            {
                string userId;
                do
                {
                    userId = random.Next(100000, 999999).ToString();
                    attempts++;

                    if (attempts > 10)
                    {
                        _logger.LogError("GenerateUserId: Muitas tentativas de gerar ID único");
                        userId = Guid.NewGuid().ToString().Substring(0, 8);
                        break;
                    }
                } while (_teamUsers.Values.Any(roomUsers => roomUsers.ContainsKey(userId)));

                return userId;
            }
        }

        // ✅ MÉTODO: Obter time atual do usuário
        public (bool found, string teamId, string teamName) GetUserCurrentTeam(string roomCode, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return (false, string.Empty, string.Empty);
            }

            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
            {
                return (false, string.Empty, string.Empty);
            }

            var user = GetAllUsersInRoom(roomCode).FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return (false, string.Empty, string.Empty);
            }

            var team = room.Teams.FirstOrDefault(t => t.Id == user.TeamId);
            return (true, user.TeamId ?? string.Empty, team?.Name ?? string.Empty);
        }

        // ✅ MÉTODO: Verificar se usuário pode acessar um time específico
        public (bool canAccess, string message) CanUserAccessTeam(string roomCode, string teamId, string userId)
        {
            var room = RoomManager.GetRoom(roomCode);
            if (room == null)
            {
                return (false, "Sala não encontrada");
            }

            // Se jogo não iniciou, qualquer um pode acessar
            if (room.GameStatus != "investigation" && room.GameStatus != "results")
            {
                return (true, "Acesso permitido");
            }

            // Jogo iniciou - verificar se usuário está neste time
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "O jogo já iniciou. Novos jogadores não podem entrar.");
            }

            var user = GetAllUsersInRoom(roomCode).FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return (false, "O jogo já iniciou. Novos jogadores não podem entrar.");
            }

            if (user.TeamId != teamId)
            {
                var userTeam = room.Teams.FirstOrDefault(t => t.Id == user.TeamId);
                return (false, $"Você está no {userTeam?.Name ?? "outro time"}. Não é possível trocar de time durante o jogo.");
            }

            return (true, "Bem-vindo de volta!");
        }

        // ✅ MÉTODO: Atualizar nome do usuário
        public (bool success, string message, string userName) UpdateUserName(UpdateUserNameViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.UserName) || model.UserName.Trim().Length < 2)
                {
                    return (false, "Nome deve ter pelo menos 2 caracteres.", string.Empty);
                }

                lock (_userLock)
                {
                    if (_teamUsers.ContainsKey(model.RoomCode) && _teamUsers[model.RoomCode].ContainsKey(model.UserId))
                    {
                        var user = _teamUsers[model.RoomCode][model.UserId];
                        user.Name = model.UserName.Trim();
                        user.LastActivity = DateTime.Now;

                        _logger.LogInformation($"Nome do usuário atualizado: {model.UserId} -> {model.UserName}");

                        return (true, "Nome atualizado com sucesso!", user.Name);
                    }

                    return (false, "Usuário não encontrado", string.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar nome do usuário");
                return (false, "Erro ao atualizar nome", string.Empty);
            }
        }

        // ✅ MÉTODO: Remover usuário
        public (bool success, string message) KickUser(string roomCode, string userId)
        {
            if (userId == "*")
            {
                lock (_userLock)
                {
                    if (_teamUsers.ContainsKey(roomCode))
                    {
                        _teamUsers[roomCode].Clear();
                    }
                }

                _logger.LogInformation($"Todos os usuários foram removidos da sala {roomCode}");
                return (true, "Todos os usuários foram removidos");
            }

            lock (_userLock)
            {
                if (_teamUsers.ContainsKey(roomCode) && _teamUsers[roomCode].ContainsKey(userId))
                {
                    var user = _teamUsers[roomCode][userId];
                    _teamUsers[roomCode].Remove(userId);

                    _logger.LogInformation($"Usuário {user.Name} (ID: {userId}) removido da sala {roomCode}");
                    return (true, $"Usuário {user.Name} removido com sucesso");
                }

                return (false, "Usuário não encontrado");
            }
        }

        // ✅ MÉTODO: Obter usuários de um time
        public List<TeamUser> GetTeamUsers(string roomCode, string teamId)
        {
            CheckAndRemoveInactiveUsers(roomCode);

            lock (_userLock)
            {
                if (!_teamUsers.ContainsKey(roomCode))
                {
                    return new List<TeamUser>();
                }

                return _teamUsers[roomCode]
                    .Where(u => u.Value.TeamId == teamId)
                    .Select(u => u.Value)
                    .Where(u => !string.IsNullOrEmpty(u.Id))
                    .ToList();
            }
        }

        // ✅ MÉTODO: Obter todos os usuários da sala
        public List<TeamUser> GetAllUsersInRoom(string roomCode)
        {
            CheckAndRemoveInactiveUsers(roomCode);

            lock (_userLock)
            {
                return _teamUsers.ContainsKey(roomCode) ?
                    _teamUsers[roomCode].Values.ToList() :
                    new List<TeamUser>();
            }
        }

        // ✅ MÉTODO: Verificar e remover usuários inativos
        public void CheckAndRemoveInactiveUsers(string roomCode)
        {
            lock (_userLock)
            {
                if (!_teamUsers.ContainsKey(roomCode)) return;

                var room = RoomManager.GetRoom(roomCode);
                if (room == null) return;

                var usersToRemove = new List<string>();
                var currentTime = DateTime.Now;

                foreach (var userEntry in _teamUsers[roomCode])
                {
                    var user = userEntry.Value;

                    // Remover por inatividade (5 minutos sem atividade)
                    if ((currentTime - user.LastActivity).TotalMinutes > 5)
                    {
                        usersToRemove.Add(userEntry.Key);
                        _logger.LogInformation($"Usuário {user.Name} removido por inatividade");
                        continue;
                    }

                    // Remover por falta de votação em rodadas consecutivas
                    if (room.GameStatus == "results" && !user.HasVoted)
                    {
                        user.MissedVotes++;

                        if (user.MissedVotes > 1)
                        {
                            usersToRemove.Add(userEntry.Key);
                            _logger.LogInformation($"Usuário {user.Name} removido por inatividade após {user.MissedVotes} rodadas sem votar");
                        }
                        else
                        {
                            _logger.LogInformation($"Usuário {user.Name} perdeu {user.MissedVotes} rodada(s) sem votar");
                        }
                    }
                }

                foreach (var userId in usersToRemove)
                {
                    _teamUsers[roomCode].Remove(userId);
                }
            }
        }

        // ✅ MÉTODO: Limpar todos os usuários
        public void ClearAllUsers()
        {
            lock (_userLock)
            {
                _teamUsers.Clear();
                _logger.LogInformation("Todos os usuários foram limpos de todas as salas");
            }
        }

        // ✅ MÉTODO: Atualizar atividade do usuário
        public void UpdateUserActivity(string roomCode, string userId)
        {
            lock (_userLock)
            {
                if (_teamUsers.ContainsKey(roomCode) && _teamUsers[roomCode].ContainsKey(userId))
                {
                    _teamUsers[roomCode][userId].LastActivity = DateTime.Now;
                    _teamUsers[roomCode][userId].IsConnected = true;
                }
            }
        }

        // ✅ MÉTODO: Verificar se usuário é líder
        public bool IsUserLeader(string roomCode, string userId)
        {
            lock (_userLock)
            {
                if (_teamUsers.ContainsKey(roomCode) && _teamUsers[roomCode].ContainsKey(userId))
                {
                    return _teamUsers[roomCode][userId].IsLeader;
                }
                return false;
            }
        }

        // ✅ MÉTODO: Promover usuário a líder
        public (bool success, string message) PromoteToLeader(string roomCode, string userId)
        {
            lock (_userLock)
            {
                if (!_teamUsers.ContainsKey(roomCode) || !_teamUsers[roomCode].ContainsKey(userId))
                {
                    return (false, "Usuário não encontrado");
                }

                var user = _teamUsers[roomCode][userId];
                var teamId = user.TeamId;

                // Remover liderança de todos no time
                foreach (var teamUser in _teamUsers[roomCode].Values.Where(u => u.TeamId == teamId))
                {
                    teamUser.IsLeader = false;
                }

                // Promover novo líder
                user.IsLeader = true;
                user.LastActivity = DateTime.Now;

                _logger.LogInformation($"Usuário {user.Name} promovido a líder do time {teamId}");

                return (true, $"{user.Name} é agora o líder da equipe");
            }
        }
    }
}