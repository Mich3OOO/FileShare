using FileShare.Api.Data;
using FileShare.Api.Services;

namespace FileShare.Api.Endpoints;

public static class CodeEndpoints
{
    public static IEndpointRouteBuilder MapCodeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/codes", async (AppDbContext db, CodeService codes) =>
        {
            var active = await codes.GetActiveAsync();
            return Results.Ok(active.Select(c => new CodeDto(c.Id, c.Code, c.IsSingleUse, c.CreatedAt)));
        })
        .RequireAuthorization();

        app.MapPost("/api/codes", async (CodeService codes, CreateCodeRequest request) =>
        {
            var entity = await codes.CreateAsync(request.IsSingleUse);
            return Results.Ok(new CodeDto(entity.Id, entity.Code, entity.IsSingleUse, entity.CreatedAt));
        })
        .RequireAuthorization();

        app.MapDelete("/api/codes/{id:guid}", async (Guid id, CodeService codes) =>
            await codes.DeleteAsync(id)
                ? Results.Ok(new { ok = true })
                : EndpointHelpers.Error("Code not found.", StatusCodes.Status404NotFound))
        .RequireAuthorization();

        return app;
    }
}