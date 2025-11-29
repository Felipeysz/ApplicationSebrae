using ApplicationSebrae.Models;

namespace ApplicationSebrae.Services
{
    public static class RoomManager
    {
        private static Dictionary<string, GameRoom> _rooms = new Dictionary<string, GameRoom>();
        private static readonly object _lock = new object();

        public static string CreateRoom(string roomName, List<TeamInfo>? customTeams = null, List<Dossier>? customDossiers = null)
        {
            lock (_lock)
            {
                string roomCode = GenerateRoomCode();

                var room = new GameRoom
                {
                    RoomCode = roomCode,
                    RoomName = roomName,
                    CreatedAt = DateTime.Now,
                    Teams = customTeams ?? GetDefaultTeams(),
                    CustomDossiers = customDossiers ?? new List<Dossier>(),
                    UseCustomDossiers = customDossiers != null && customDossiers.Any()
                };

                _rooms[roomCode] = room;
                return roomCode;
            }
        }

        public static GameRoom? GetRoom(string roomCode)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(roomCode))
                {
                    return null;
                }

                return _rooms.TryGetValue(roomCode, out var room) ? room : null;
            }
        }

        public static List<GameRoom> GetAllRooms()
        {
            lock (_lock)
            {
                return _rooms.Values.ToList();
            }
        }

        public static bool RoomExists(string roomCode)
        {
            lock (_lock)
            {
                return _rooms.ContainsKey(roomCode);
            }
        }

        public static bool ResetRoom(string roomCode)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(roomCode, out var room))
                {
                    room.CurrentRound = 0;
                    room.GameStatus = "setup";

                    foreach (var team in room.Teams)
                    {
                        team.Score = 0;
                        team.RoundScores.Clear();
                        team.Responses.Clear();
                        foreach (var user in team.Users)
                        {
                            user.HasVoted = false;
                            user.MissedVotes = 0;
                        }
                    }

                    return true;
                }
                return false;
            }
        }

        public static bool DeleteRoom(string roomCode)
        {
            lock (_lock)
            {
                return _rooms.Remove(roomCode);
            }
        }

        public static void ResetAllRooms()
        {
            lock (_lock)
            {
                foreach (var room in _rooms.Values)
                {
                    room.CurrentRound = 0;
                    room.GameStatus = "setup";

                    foreach (var team in room.Teams)
                    {
                        team.Score = 0;
                        team.RoundScores.Clear();
                        team.Responses.Clear();
                        foreach (var user in team.Users)
                        {
                            user.HasVoted = false;
                            user.MissedVotes = 0;
                        }
                    }
                }
            }
        }

        public static void UpdateTeamActivity(string roomCode, string teamId)
        {
            lock (_lock)
            {
                if (_rooms.ContainsKey(roomCode))
                {
                    var team = _rooms[roomCode].Teams.FirstOrDefault(t => t.Id == teamId);
                    if (team != null)
                    {
                        team.LastActivity = DateTime.Now;
                    }
                }
            }
        }

        public static void UpdateUserActivity(string roomCode, string teamId, string userId)
        {
            lock (_lock)
            {
                if (_rooms.ContainsKey(roomCode))
                {
                    var team = _rooms[roomCode].Teams.FirstOrDefault(t => t.Id == teamId);
                    if (team != null)
                    {
                        var user = team.Users.FirstOrDefault(u => u.Id == userId);
                        if (user != null)
                        {
                            user.LastVoteTime = DateTime.Now;
                            user.IsConnected = true;
                        }

                        team.LastActivity = DateTime.Now;
                    }
                }
            }
        }

        public static void CleanInactiveRooms(TimeSpan maxInactivity)
        {
            lock (_lock)
            {
                var inactiveRoomCodes = _rooms
                    .Where(r => DateTime.Now - r.Value.LastActivity() > maxInactivity)
                    .Select(r => r.Key)
                    .ToList();

                foreach (var roomCode in inactiveRoomCodes)
                {
                    _rooms.Remove(roomCode);
                }
            }
        }

        private static string GenerateRoomCode()
        {
            var random = new Random();
            string code;

            do
            {
                code = random.Next(100000, 999999).ToString();
            } while (_rooms.ContainsKey(code));

            return code;
        }

        private static List<TeamInfo> GetDefaultTeams()
        {
            return new List<TeamInfo>
            {
                new TeamInfo {
                    Id = "team_1",
                    Name = "Equipe A",
                    Icon = "🚀",
                    Score = 0,
                    Users = new List<TeamUser>(),
                    LastActivity = DateTime.Now
                },
                new TeamInfo {
                    Id = "team_2",
                    Name = "Equipe B",
                    Icon = "🌟",
                    Score = 0,
                    Users = new List<TeamUser>(),
                    LastActivity = DateTime.Now
                },
                new TeamInfo {
                    Id = "team_3",
                    Name = "Equipe C",
                    Icon = "🎯",
                    Score = 0,
                    Users = new List<TeamUser>(),
                    LastActivity = DateTime.Now
                }
            };
        }
    }
}