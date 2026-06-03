using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagementSystem.ViewComponents
{
    public class ShowMyPaymentsNavViewComponent : ViewComponent
    {
        private readonly ResidentProfileService _residentProfileService;

        public ShowMyPaymentsNavViewComponent(ResidentProfileService residentProfileService)
        {
            _residentProfileService = residentProfileService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(false);
            }

            if (User.IsInRole("Resident"))
            {
                return View(true);
            }

            if (User.IsInRole("Admin"))
            {
                var flat = await _residentProfileService.GetFlatForUserAsync(UserClaimsPrincipal);
                return View(!string.IsNullOrEmpty(flat));
            }

            return View(false);
        }
    }
}
