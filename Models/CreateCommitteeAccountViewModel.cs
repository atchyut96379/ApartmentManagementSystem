using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class CreateCommitteeAccountViewModel
    {
        [Required]
        public string Designation { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Name")]
        public string OwnerName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Flat number")]
        public string FlatNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Mobile number (login username)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Notification email (optional)")]
        public string? Email { get; set; }
    }
}
