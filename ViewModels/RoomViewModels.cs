using ApplicationSebrae.Models;
using System.ComponentModel.DataAnnotations;

namespace ApplicationSebrae.ViewModels
{
    public class RoomAccessViewModel
    {
        [Required(ErrorMessage = "Código da sala é obrigatório")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "O código deve ter 6 dígitos")]
        public string RoomCode { get; set; } = string.Empty;
    }

    public class CreateRoomViewModel
    {
        [Required(ErrorMessage = "Nome da sala é obrigatório")]
        public string RoomName { get; set; } = string.Empty;

        public List<CustomTeamViewModel> CustomTeams { get; set; } = new();
        public bool UseCustomTeams { get; set; } = false;
        public bool UseCustomDossiers { get; set; } = false;
        public List<CustomDossierViewModel> CustomDossiers { get; set; } = new();
    }

    public class FacilitatorDashboardViewModel
    {
        public List<GameRoom> ActiveRooms { get; set; } = new();
    }
    public class ManageQuestionsViewModel
    {
        public string RoomCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public List<Dossier> CustomDossiers { get; set; } = new();
        public bool HasQuestions { get; set; }
        public bool CanStartGame { get; set; }
    }

    public class AddQuestionRequest
    {
        public string RoomCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Challenge { get; set; }
        public string? Objective { get; set; }
        public List<string> Alternatives { get; set; } = new();
        public List<int> CorrectAnswers { get; set; } = new();
        public string? Explanation { get; set; }
    }

    public class EditQuestionRequest : AddQuestionRequest
    {
        public int QuestionIndex { get; set; }
    }

    public class DeleteQuestionRequest
    {
        public string RoomCode { get; set; } = string.Empty;
        public int QuestionIndex { get; set; }
    }
}