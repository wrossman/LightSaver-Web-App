using Microsoft.AspNetCore.Antiforgery;
public static class SecurityEndpoints
{
    public static void MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/security")
            .RequireRateLimiting("by-ip-policy");

        group.MapGet("/csrf", ProvideCsrfToken);
    }

    public static IResult ProvideCsrfToken(HttpContext context, IAntiforgery af)
    {
        var tokens = af.GetAndStoreTokens(context);

        CsrfResponse tokenResponse = new()
        {
            Token = tokens.RequestToken
        };

        return Results.Json(tokenResponse);
    }
}