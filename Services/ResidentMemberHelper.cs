using ApartmentManagementSystem.Models;

namespace ApartmentManagementSystem.Services
{
    public static class ResidentMemberHelper
    {
        public static void ApplyMemberType(Resident resident, ResidentMemberType memberType)
        {
            resident.MemberType = memberType;
            resident.IsOwner = memberType == ResidentMemberType.Owner ||
                               memberType == ResidentMemberType.AssociationAdmin;

            if (memberType != ResidentMemberType.Tenant)
            {
                resident.PropertyOwnerName = null;
                resident.OwnerContactNumber = null;
            }

            if (memberType != ResidentMemberType.AssociationAdmin)
            {
                resident.AssociationDesignation = null;
            }
        }

        public static bool CountsAsInHouseOwner(ResidentMemberType memberType)
        {
            return memberType == ResidentMemberType.Owner ||
                   memberType == ResidentMemberType.AssociationAdmin;
        }

        public static string GetMemberTypeLabel(Resident resident)
        {
            return resident.MemberType switch
            {
                ResidentMemberType.Tenant => "Tenant",
                ResidentMemberType.AssociationAdmin => "Association Admin",
                _ => "Owner"
            };
        }
    }
}
