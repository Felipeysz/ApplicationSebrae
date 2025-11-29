using System.ComponentModel.DataAnnotations;

namespace ApplicationSebrae.ViewModels
{
    public class JoinTeamViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string TeamId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome é obrigatório")]
        [MinLength(2, ErrorMessage = "Nome deve ter pelo menos 2 caracteres")]
        public string UserName { get; set; } = string.Empty;
    }

    public class VoteSubmissionViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string TeamId { get; set; } = string.Empty;

        public List<int> SelectedAlternatives { get; set; } = new List<int>();
    }

    public class AnswerSubmissionViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        public int Round { get; set; }

        public List<int>? SelectedAlternatives { get; set; }

        [Required]
        public string TeamId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }

    public class KickUserViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class VoteKickViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string TargetUserId { get; set; } = string.Empty;

        [Required]
        public string VoterUserId { get; set; } = string.Empty;
    }

    public class UpdateUserNameViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome é obrigatório")]
        [MinLength(2, ErrorMessage = "Nome deve ter pelo menos 2 caracteres")]
        public string UserName { get; set; } = string.Empty;
    }

    public class TeamDisconnectViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string TeamId { get; set; } = string.Empty;
    }

    public class TeamPingViewModel
    {
        [Required]
        public string RoomCode { get; set; } = string.Empty;

        [Required]
        public string TeamId { get; set; } = string.Empty;
    }

    public class TeamAnswerRequestViewModel
    {
        public string RoomCode { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public int Round { get; set; }
        public List<int> SelectedAlternatives { get; set; } = new List<int>();
    }
}