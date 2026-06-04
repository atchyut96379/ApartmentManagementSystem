using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class CreateResidentViewModel
    {
        [Required]
        [Display(Name = "Flat Number")]
        public string FlatNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Resident Name")]
        public string OwnerName { get; set; } = string.Empty;

        [Display(Name = "Login mobile")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Notification email (optional)")]
        public string Email { get; set; } = string.Empty;

        public ResidentMemberType MemberType { get; set; } = ResidentMemberType.Owner;

        [Display(Name = "Committee role")]
        public string? AssociationDesignation { get; set; }

        [Display(Name = "Property Owner Name")]
        public string? PropertyOwnerName { get; set; }

        [Display(Name = "Owner Contact Number")]
        public string? OwnerContactNumber { get; set; }

        [Display(Name = "Create login account (default password: apartment prefix + flat number)")]
        public bool CreateLoginAccount { get; set; }

        [Display(Name = "Committee admin access (can add residents, import, send reminders)")]
        public bool GrantCommitteeAdminAccess { get; set; }

        public List<string> AvailableDesignations { get; set; } = new();
    }
}
