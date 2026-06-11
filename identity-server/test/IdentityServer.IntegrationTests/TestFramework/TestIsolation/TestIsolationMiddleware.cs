// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

/// <summary>
/// Dispatcher middleware that reads the request hostname and routes the request
/// to the appropriate per-test pipeline.
/// <para>
/// Expected hostname format: <c>{testId}-{serverName}.dev.localhost</c> or
/// <c>{testId}.dev.localhost</c> (no server name). Requests that do not match
/// this pattern fall through to the default pipeline.
/// </para>
/// </summary>
internal sealed class TestIsolationMiddleware(
    RequestDelegate next,
    TestIsolationService service)
{
    private const string DevLocalhostSuffix = ".dev.localhost";

    public async Task InvokeAsync(HttpContext context)
    {
        TestRegistration? registration = null;
        var host = context.Request.Host.Host;

        var matched = (TryParseTestKey(host, out var testId, out var serverName)
                && service.TryGetRegistration(testId, serverName, out registration))
            || service.TryGetRegistrationByHost(host, out registration);

        if (!matched || registration is null)
        {
            await next(context);
            return;
        }

        var composite = new CompositeServiceProvider(
            registration.ServiceProvider, service.GlobalServices);
#pragma warning disable CA2000 // Ownership transferred to HttpResponse via RegisterForDispose
        var scope = composite.CreateScope();
#pragma warning restore CA2000

        // Register for disposal immediately to ensure cleanup even if
        // subsequent code throws. RegisterForDispose disposes the scope
        // when the response/request is completed or aborted.
        context.Response.RegisterForDispose(scope);

        // Set the per-test service provider for the duration of this request.
        // We intentionally do NOT restore the original RequestServices afterward.
        // ASP.NET Core's Response.OnStarting callbacks (used by e.g.
        // IdentityServerAuthenticationService for post-signout cleanup) fire
        // after the pipeline returns but before the response is sent.
        // Restoring the original provider in a finally block would cause those
        // callbacks to resolve services from the wrong (global) container,
        // leading to failures when the per-test auth handlers are needed.
        // Since HttpContext is request-scoped and not reused, this is safe.
        context.RequestServices = scope.ServiceProvider;

        // Do NOT set HttpContextAccessor.HttpContext here. The hosting layer
        // (DefaultHttpContextFactory.Initialize) already sets it correctly.
        // HttpContextAccessor uses a static AsyncLocal, so re-assigning it
        // here corrupts the parent async frame's HttpContextHolder — causing
        // OnStarting callbacks to see a null HttpContext.

        await registration.Pipeline(context);
    }

    /// <summary>
    /// Parses a hostname of the form <c>{testId}.dev.localhost</c> or
    /// <c>{testId}-{serverName}.dev.localhost</c> into its components.
    /// The server name may itself contain hyphens; only the first hyphen is used
    /// as the separator between the numeric test ID and the server name.
    /// </summary>
    internal static bool TryParseTestKey(
        string host,
        out int testId,
        out string serverName)
    {
        testId = 0;
        serverName = "";

        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (!host.EndsWith(DevLocalhostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefix = host[..^DevLocalhostSuffix.Length];
        if (string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        var dashIndex = prefix.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex < 0)
        {
            // No server name: "{testId}.dev.localhost"
            return int.TryParse(prefix, out testId);
        }

        // Has server name: "{testId}-{serverName}.dev.localhost"
        var idPart = prefix[..dashIndex];
        if (!int.TryParse(idPart, out testId))
        {
            return false;
        }

        serverName = prefix[(dashIndex + 1)..].ToUpperInvariant();
        return !string.IsNullOrEmpty(serverName);
    }
}
