namespace ApartmentManagementSystem.Models
{
    public class ResidentsIndexViewModel
    {
        public List<ResidentIndexItem> Residents { get; set; } = new();
    }

    public class ResidentIndexItem
    {
        public Resident Resident { get; set; } = null!;

        public bool HasLogin { get; set; }

        public bool MustChangePassword { get; set; }
    }
}
