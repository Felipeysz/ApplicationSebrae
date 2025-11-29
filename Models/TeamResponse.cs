namespace ApplicationSebrae.Models
{
    public class TeamResponse
    {
        public List<int> SelectedAlternatives { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public int Score { get; set; }
        public List<string> UserVotes { get; set; } = new();
        public int TotalUsers { get; set; }

        // ✅ ADICIONAR: Para debug e verificação
        public List<int> CorrectAnswers { get; set; } = new();
        public List<int> UserAnswers { get; set; } = new();
        public object ShuffledAlternatives { get; internal set; }
    }
}