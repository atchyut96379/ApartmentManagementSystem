using ApartmentManagementSystem.Models;
using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.ViewComponents
{
    public class AssociationRoleViewComponent : ViewComponent
    {
        private readonly ResidentProfileService _residentProfileService;

        public AssociationRoleViewComponent(ResidentProfileService residentProfileService)
        {
            _residentProfileService = residentProfileService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var resident = await _residentProfileService.GetCurrentResidentAsync(HttpContext.User);
            if (resident?.MemberType != ResidentMemberType.AssociationAdmin ||
                string.IsNullOrWhiteSpace(resident.AssociationDesignation))
            {
                return Content(string.Empty);
            }

            return View("Default", resident.AssociationDesignation);
        }
    }
}
