using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
namespace CheckInApp.Api.Security;

public sealed class AspNetRoleAttribute : AuthorizeAttribute
{
    public const string Prefix = "Permission:"; public AspNetRoleAttribute(Permission permission)
    {
        Policy = Prefix + ((long)permission);
    }
}
public record PermissionRequirement(Permission Permission) : IAuthorizationRequirement;
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string name) => name.StartsWith(AspNetRoleAttribute.Prefix) && long.TryParse(name[AspNetRoleAttribute.Prefix.Length..], out var mask) ? Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement((Permission)mask)).Build()) : base.GetPolicyAsync(name);
}
public sealed class PermissionHandler(AccessService access) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirst("sub")?.Value, out var user))
            try
            {
                await access.Require(user, requirement.Permission);
                context.Succeed(requirement);
            }
            catch (Application.AppException) { context.Fail(); }
    }
}
