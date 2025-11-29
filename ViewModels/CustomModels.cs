using System.ComponentModel.DataAnnotations;

namespace ApplicationSebrae.ViewModels
{
    public class CustomTeamViewModel
    {
        [Required(ErrorMessage = "Nome da equipe é obrigatório")]
        public string Name { get; set; } = string.Empty;

        public string Icon { get; set; } = "🚀";
    }

    public class CustomDossierViewModel
    {
        [Required(ErrorMessage = "Título é obrigatório")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome é obrigatório")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Descrição é obrigatória")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Desafio é obrigatório")]
        public string Challenge { get; set; } = string.Empty;

        [Required(ErrorMessage = "Objetivo é obrigatório")]
        public string Objective { get; set; } = string.Empty;

        public List<string> Alternatives { get; set; } = new();
        public List<int> CorrectAnswers { get; set; } = new();
        public string Explanation { get; set; } = string.Empty;
    }
}