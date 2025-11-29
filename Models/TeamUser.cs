namespace ApplicationSebrae.Models
{
    public class TeamUser
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? TeamId { get; set; }
        public bool HasVoted { get; set; }
        public bool IsLeader { get; set; }
        public bool IsConnected { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.Now;
        public DateTime LastVoteTime { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.Now;
        public int MissedVotes { get; set; }
        public List<int>? CurrentVotes { get; set; } = new List<int>();
        public DateTime JoinTime { get; internal set; }
    }
}