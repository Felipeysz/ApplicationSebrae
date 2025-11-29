using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ApplicationSebrae.Models
{
    public class GameRoom
    {
        public string RoomCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int CurrentRound { get; set; }
        public string GameStatus { get; set; } = "setup"; // setup, presentation, investigation, results, finished
        public List<TeamInfo> Teams { get; set; } = new List<TeamInfo>();

        // Dossiês personalizados
        public List<Dossier> CustomDossiers { get; set; } = new List<Dossier>();
        public bool UseCustomDossiers { get; set; } = false;

        // ✅ NOVO: Timestamp do último reset de rodada
        public DateTime? LastResetTime { get; set; }

        public DateTime LastActivity()
        {
            return Teams.Any()
                ? Teams.Max(t => t.LastActivity)
                : CreatedAt;
        }
    }
}