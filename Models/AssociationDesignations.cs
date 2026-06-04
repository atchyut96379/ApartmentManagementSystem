namespace ApartmentManagementSystem.Models
{
    public static class AssociationDesignations
    {
        public const int MaxAssociationAdmins = 5;

        public static readonly string[] All =
        {
            "President",
            "Vice President",
            "Secretary",
            "Joint Secretary",
            "Treasurer"
        };

        /// <summary>Committee roles allowed to assign or change other members' designations.</summary>
        public static readonly string[] DesignationChangeAuthorized =
        {
            "President",
            "Secretary"
        };

        public static bool CanChangeMemberDesignations(string? actorDesignation)
        {
            if (string.IsNullOrWhiteSpace(actorDesignation))
            {
                return false;
            }

            return DesignationChangeAuthorized.Contains(
                actorDesignation.Trim(),
                StringComparer.OrdinalIgnoreCase);
        }

        public static string Display(string? designation)
        {
            return string.IsNullOrWhiteSpace(designation) ? "—" : designation;
        }
    }
}
