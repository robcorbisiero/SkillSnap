using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapGet("/csrf", (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return new CsrfToken(antiforgery.GetAndStoreTokens(ctx).RequestToken!);
        });
        group.MapGet("/me", async (HttpContext ctx, UserManager<IdentityUser> users) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var user = await users.GetUserAsync(ctx.User);
            return user is null ? Results.Unauthorized() : Results.Ok(new AccountInfo(user.Email!));
        }).RequireAuthorization();
        group.MapPost("/register", RegisterAsync).AddEndpointFilter<CsrfFilter>().RequireRateLimiting("accounts");
        group.MapPost("/login", LoginAsync).AddEndpointFilter<CsrfFilter>().RequireRateLimiting("accounts");
        group.MapPost("/logout", async (SignInManager<IdentityUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
    }

    private static async Task<IResult> RegisterAsync(AccountCredentials input, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        if (!Valid(input)) return Results.Problem(statusCode: 400, title: "Enter a valid email and a password of 10–128 characters.");
        var email = input.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return Results.Problem(statusCode: 400, title: "Couldn’t create the account. Use a unique email and a password with uppercase, lowercase, a number, and a symbol.");
        await signIn.SignInAsync(user, isPersistent: true);
        return Results.Ok(new AccountInfo(email));
    }

    private static async Task<IResult> LoginAsync(AccountCredentials input, SignInManager<IdentityUser> signIn)
    {
        if (!Valid(input)) return Results.Unauthorized();
        var result = await signIn.PasswordSignInAsync(input.Email.Trim(), input.Password, isPersistent: true, lockoutOnFailure: true);
        return result.Succeeded ? Results.Ok(new AccountInfo(input.Email.Trim())) : Results.Unauthorized();
    }

    private static bool Valid(AccountCredentials input) =>
        Validator.TryValidateObject(input, new ValidationContext(input), [], validateAllProperties: true);
}
