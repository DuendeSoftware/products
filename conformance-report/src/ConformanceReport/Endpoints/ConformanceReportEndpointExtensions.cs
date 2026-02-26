// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Endpoints;

/// <summary>
/// Extension methods for mapping conformance assessment endpoints.
/// </summary>
public static class ConformanceReportEndpointExtensions
{
    /// <summary>
    /// Maps the conformance assessment endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>A builder for configuring the endpoint group.</returns>
    public static RouteGroupBuilder MapConformanceReport(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;
        var basePath = $"/{options.PathPrefix.Trim('/')}/{ConformanceReportConstants.FeaturePath}";

        var group = endpoints.MapGroup(basePath);

        // HTML endpoint - requires custom authorization policy
        _ = group.MapGet("", async (ConformanceReportEndpoint endpoint, HttpContext context, Ct ct) =>
            await endpoint.GetHtmlReportAsync(context, ct))
            .RequireAuthorization(options.AuthorizationPolicyName)
            .WithName("GetConformanceHtmlReport")
            .WithDescription("Gets the conformance assessment report as an HTML page")
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return group;
    }
}
