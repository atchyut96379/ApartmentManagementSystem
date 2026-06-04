using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApartmentManagementSystem.Filters
{
    public class RequireCommitteeAdminAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var access = context.HttpContext.RequestServices
                .GetRequiredService<CommitteeAccessService>();

            if (!await access.CanManageResidentsAsync(context.HttpContext.User))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
