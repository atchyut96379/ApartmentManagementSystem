using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [Display(Name = "Mobile number (login username)")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Flat number")]
        public string FlatNumber { get; set; } = string.Empty;
    }
}
