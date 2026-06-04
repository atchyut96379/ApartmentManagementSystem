using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.ViewComponents
{
    public class UserAccessViewComponent : ViewComponent
    {
        private readonly CommitteeAccessService _access;

        public UserAccessViewComponent(CommitteeAccessService access)
        {
            _access = access;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _access.GetUserAsync(HttpContext.User);
            return View(new UserAccessInfo
            {
                IsSystemAdmin = _access.IsSystemAdmin(user),
                IsCommitteeAdmin = await _access.IsCommitteeAdminAsync(user),
                CanManageResidents = await _access.CanManageResidentsAsync(HttpContext.User)
            });
        }
    }

    public class UserAccessInfo
    {
        public bool IsSystemAdmin { get; set; }

        public bool IsCommitteeAdmin { get; set; }

        public bool CanManageResidents { get; set; }

        public bool ShowAdminMenus =>
            IsSystemAdmin || IsCommitteeAdmin;
    }
}
