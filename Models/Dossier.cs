namespace ApplicationSebrae.Models
{
    public class Dossier
    {
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Challenge { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public List<string> Alternatives { get; set; } = new List<string>();
        public List<int> CorrectAnswers { get; set; } = new List<int>();
        public string Explanation { get; set; } = string.Empty;
    }
}