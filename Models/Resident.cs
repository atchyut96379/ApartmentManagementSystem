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

        [Display(Name = "Login mobile")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Notification email")]
        public string Email { get; set; }

        public string? UserId { get; set; }

        public bool IsOwner { get; set; }

        public ResidentMemberType MemberType { get; set; } = ResidentMemberType.Owner;

        /// <summary>Committee role when MemberType is AssociationAdmin (max 5 per society).</summary>
        public string? AssociationDesignation { get; set; }

        /// <summary>Landlord name when the resident is a tenant (IsOwner=false).</summary>
        public string? PropertyOwnerName { get; set; }

        /// <summary>Landlord contact when tenant has no owner name in import data.</summary>
        public string? OwnerContactNumber { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
