using IamMock.Api.Data;

namespace IamMock.Api.Endpoints;

public static class LicenseEndpoints
{
    public static IEndpointRouteBuilder MapLicenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/licenses").WithTags("Licenses");

        group.MapGet("/", (MockDataStore db, Guid? tenantId) => db.GetLicenses(tenantId))
            .WithSummary("List licenses, optionally filtered by tenant.");

        group.MapGet("/{id:guid}", (Guid id, MockDataStore db) =>
                db.GetLicense(id) is { } license ? Results.Ok(license) : Results.NotFound())
            .WithSummary("Get a single license by id.");

        return app;
    }
}
