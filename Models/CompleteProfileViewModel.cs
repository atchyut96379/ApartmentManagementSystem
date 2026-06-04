using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class CompleteProfileViewModel
    {
        [Display(Name = "Login mobile")]
        public string LoginId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Flat Number")]
        public string FlatNumber { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }
    }
}
