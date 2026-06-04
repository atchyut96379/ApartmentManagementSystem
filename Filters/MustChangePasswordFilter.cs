using ApartmentManagementSystem.Identity;
using SystemAdminConstants = ApartmentManagementSystem.Identity.SystemAdminConstants;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApartmentManagementSystem.Filters
{
    public class MustChangePasswordFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "ChangePasswordRequired",
            "Logout",
            "AccessDenied",
            "Login",
            "ForgotPassword",
            "VerifyForgotPasswordOtp",
            "ResetForgottenPassword"
        };

        private readonly UserManager<ApplicationUser> _userManager;

        public MustChangePasswordFilter(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (controller != null &&
                controller.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                action != null &&
                AllowedActions.Contains(action))
            {
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(context.HttpContext.User);
            if (user != null && user.MustChangePassword)
            {
                context.Result = new RedirectToActionResult(
                    "ChangePasswordRequired",
                    "Account",
                    null);
                return;
            }

            await next();
        }
    }
}
