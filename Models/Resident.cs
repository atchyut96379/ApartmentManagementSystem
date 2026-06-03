using System.ComponentModel.DataAnnotations;

namespace ApartmentManagementSystem.Models
{
    public class Resident
    {
    public int Id { get; set; }
        
        [Required]
        public string FlatNumber { get; set; }

        [Required]
        public string OwnerName { get; set; }

        public string PhoneNumber { get; set; }

        public string Email { get; set; }

        public string? UserId { get; set; }

        public bool IsOwner { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
