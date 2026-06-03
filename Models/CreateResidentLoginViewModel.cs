using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class CreateResidentLoginViewModel
    {
        public int ResidentId { get; set; }

        public string ResidentName { get; set; } = string.Empty;

        public string FlatNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Login email")]
        public string Email { get; set; } = string.Empty;
    }
}
