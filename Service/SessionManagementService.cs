using Microsoft.Extensions.Logging;

namespace ApplicationSebrae.Services
{
    public class SessionManagementService
    {
        private static Dictionary<string, HashSet<string>> _activeSessions = new Dictionary<string, HashSet<string>>();
        private static readonly object _sessionLock = new object();
        private readonly ILogger<SessionManagementService> _logger;

        public SessionManagementService(ILogger<SessionManagementService> logger)
        {
            _logger = logger;
        }

        public void RegisterActiveSession(string roomCode, string teamId)
        {
            lock (_sessionLock)
            {
                if (!_activeSessions.ContainsKey(roomCode))
                {
                    _activeSessions[roomCode] = new HashSet<string>();
                }
                _activeSessions[roomCode].Add(teamId);
            }
        }

        public void RemoveActiveSession(string roomCode, string teamId)
        {
            lock (_sessionLock)
            {
                if (_activeSessions.ContainsKey(roomCode))
                {
                    if (teamId == "*")
                    {
                        _activeSessions[roomCode].Clear();
                    }
                    else
                    {
                        _activeSessions[roomCode].Remove(teamId);
                    }

                    if (_activeSessions[roomCode].Count == 0)
                    {
                        _activeSessions.Remove(roomCode);
                    }
                }
            }
        }

        public int GetConnectedTeamsCount(string roomCode)
        {
            lock (_sessionLock)
            {
                if (_activeSessions.ContainsKey(roomCode))
                {
                    return _activeSessions[roomCode].Count;
                }
                return 0;
            }
        }

        // NOVO: Retorna lista de IDs dos times conectados
        public List<string> GetConnectedTeams(string roomCode)
        {
            lock (_sessionLock)
            {
                if (_activeSessions.ContainsKey(roomCode))
                {
                    var teams = _activeSessions[roomCode].ToList();
                    return teams;
                }
                return new List<string>();
            }
        }

        public bool IsTeamActive(string roomCode, string teamId)
        {
            lock (_sessionLock)
            {
                bool isActive = _activeSessions.ContainsKey(roomCode) && _activeSessions[roomCode].Contains(teamId);
                return isActive;
            }
        }

        public List<string> GetActiveSessionsForRoom(string roomCode)
        {
            lock (_sessionLock)
            {
                return _activeSessions.ContainsKey(roomCode) ?
                    _activeSessions[roomCode].ToList() :
                    new List<string>();
            }
        }

        public void ClearAllSessions()
        {
            lock (_sessionLock)
            {
                _activeSessions.Clear();
            }
        }
    }
}