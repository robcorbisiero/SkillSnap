using Microsoft.AspNetCore.Antiforgery;

namespace SkillSnap.Api.Endpoints;

public sealed class CsrfFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "Refresh the page and try again.");
        }
        return await next(context);
    }
}
