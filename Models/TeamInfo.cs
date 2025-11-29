namespace ApplicationSebrae.Models
{
    public class TeamInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = "👥";
        public int Score { get; set; }
        public List<int> RoundScores { get; set; } = new List<int>();
        public List<TeamUser> Users { get; set; } = new List<TeamUser>();
        public DateTime LastActivity { get; set; } = DateTime.Now;
        public Dictionary<int, TeamResponse> Responses { get; set; } = new Dictionary<int, TeamResponse>();

        public bool IsActive => (DateTime.Now - LastActivity).TotalMinutes < 5;
    }
}