using ApartmentManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApartmentManagementSystem.Filters
{
    /// <summary>
    /// Allows only committee President or Secretary (not system Admin, Treasurer, etc.).
    /// </summary>
    public class RequirePresidentOrSecretaryAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var access = context.HttpContext.RequestServices
                .GetRequiredService<CommitteeAccessService>();

            if (!await access.CanChangeCommitteeDesignationAsync(context.HttpContext.User))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
