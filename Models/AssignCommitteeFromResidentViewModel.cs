using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class AssignCommitteeFromResidentViewModel
    {
        [Required]
        public string Designation { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Resident from imported list")]
        public int ResidentId { get; set; }

        [EmailAddress]
        [Display(Name = "Optional email (notifications only, not for login)")]
        public string? NotificationEmail { get; set; }

        public List<ResidentPickItem> Candidates { get; set; } = new();
    }

    public class ResidentPickItem
    {
        public int Id { get; set; }

        public string FlatNumber { get; set; } = string.Empty;

        public string OwnerName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool HasLogin { get; set; }

        public string MemberTypeLabel { get; set; } = string.Empty;
    }
}
