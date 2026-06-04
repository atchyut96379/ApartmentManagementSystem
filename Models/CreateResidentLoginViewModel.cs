using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class CreateResidentLoginViewModel
    {
        public int ResidentId { get; set; }

        public string ResidentName { get; set; } = string.Empty;

        public string FlatNumber { get; set; } = string.Empty;

        [Display(Name = "Login mobile number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Optional email (notifications only, not for login)")]
        public string? NotificationEmail { get; set; }

        [Display(Name = "Committee admin access (can add residents, import, send reminders)")]
        public bool GrantCommitteeAdminAccess { get; set; }

        [Display(Name = "Committee role")]
        public string? AssociationDesignation { get; set; }

        public List<string> AvailableDesignations { get; set; } = new();
    }
}
