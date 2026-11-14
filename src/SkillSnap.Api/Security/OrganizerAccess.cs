using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace SkillSnap.Api.Security;

public sealed class OrganizerRequirement : IAuthorizationRequirement { }

public sealed class OrganizerHandler(UserManager<IdentityUser> users) : AuthorizationHandler<OrganizerRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizerRequirement requirement)
    {
        var user = await users.GetUserAsync(context.User);
        // Check the store, rather than trusting a role cached in an older login cookie.
        if (user is not null && await users.IsInRoleAsync(user, "Organizer")) context.Succeed(requirement);
    }
}

public static class OrganizerProvisioner
{
    public static async Task GrantAsync(IServiceProvider services, string email)
    {
        if (!services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            throw new InvalidOperationException("Local organizer provisioning is only available in Development.");
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email.Trim())
            ?? throw new InvalidOperationException("Create the account first. No matching account was found.");
        if (!await roles.RoleExistsAsync("Organizer"))
        {
            var created = await roles.CreateAsync(new IdentityRole("Organizer"));
            if (!created.Succeeded) throw new InvalidOperationException("Could not create the organizer role.");
        }
        if (!await users.IsInRoleAsync(user, "Organizer"))
        {
            var result = await users.AddToRoleAsync(user, "Organizer");
            if (!result.Succeeded) throw new InvalidOperationException("Could not grant organizer access.");
        }
    }
}
