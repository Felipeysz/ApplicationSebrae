using ApplicationSebrae.Models;

namespace ApplicationSebrae.ViewModels
{
    public class TeamGameViewModel
    {
        public string RoomCode { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public int CurrentRound { get; set; }
        public string GameStatus { get; set; } = string.Empty;
        public List<TeamUser> Users { get; set; } = new();
    }

    public class FacilitatorGameViewModel
    {
        public string RoomCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public List<TeamInfo> Teams { get; set; } = new();
        public int CurrentRound { get; set; }
        public string GameStatus { get; set; } = string.Empty;
        public int TotalRounds { get; set; }
        public int ConnectedTeams { get; set; }
        public int TotalUsers { get; set; }
    }
}