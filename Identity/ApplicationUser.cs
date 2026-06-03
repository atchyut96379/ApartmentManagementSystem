using Microsoft.AspNetCore.Identity;

namespace ApartmentManagementSystem.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }

        public string? FlatNumber { get; set; }
    }
}