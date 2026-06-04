namespace ApartmentManagementSystem.Identity
{
    public static class SystemAdminConstants
    {
        public const string UserName = "Admin";
        public const string Email = "admin@system.local";
        public const string DefaultPassword = "Admin";
        public const string FullName = "System Administrator";

        public static bool IsSystemAdmin(ApplicationUser? user)
        {
            if (user == null)
            {
                return false;
            }

            return string.Equals(user.UserName, UserName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(user.Email, Email, StringComparison.OrdinalIgnoreCase);
        }
    }
}
