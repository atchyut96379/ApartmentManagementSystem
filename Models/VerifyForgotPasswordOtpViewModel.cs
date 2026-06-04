using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class VerifyForgotPasswordOtpViewModel
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        [Display(Name = "6-digit code from SMS")]
        public string OtpCode { get; set; } = string.Empty;

        [Display(Name = "Masked mobile")]
        public string MaskedPhone { get; set; } = string.Empty;
    }
}
