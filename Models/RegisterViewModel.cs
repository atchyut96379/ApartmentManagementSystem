using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = "Resident";

        [Display(Name = "Flat Number")]
        public string? FlatNumber { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public bool ShowLoginLink { get; set; }
    }
}
