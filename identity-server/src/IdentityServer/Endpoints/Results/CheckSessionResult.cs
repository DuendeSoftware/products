// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// The resukt of the check session endpoint
/// </summary>
public class CheckSessionResult : EndpointResult<CheckSessionResult>
{
}


internal class CheckSessionHttpWriter : IHttpResponseWriter<CheckSessionResult>
{
    public CheckSessionHttpWriter(IdentityServerOptions options) => _options = options;

    private static readonly string CheckSessionScript = GetEmbeddedResource($"{typeof(CheckSessionHttpWriter).Namespace}.check-session-result.js");
    private IdentityServerOptions _options;
    private static volatile string FormattedHtml;
    private static readonly object Lock = new object();
    private static volatile string LastCheckSessionCookieName;

    public async Task WriteHttpResponse(CheckSessionResult result, HttpContext context)
    {
        AddCspHeaders(context);

        var html = GetHtml(_options.Authentication.CheckSessionCookieName);
        await context.Response.WriteHtmlAsync(html);
    }

    private void AddCspHeaders(HttpContext context) => context.Response.AddScriptCspHeaders(_options.Csp, IdentityServerConstants.ContentSecurityPolicyHashes.CheckSessionScript);
    private string GetHtml(string cookieName)
    {
        if (cookieName != LastCheckSessionCookieName)
        {
            lock (Lock)
            {
                if (cookieName != LastCheckSessionCookieName)
                {
                    FormattedHtml = Html.Replace("{cookieName}", cookieName)
                        .Replace("{script}", CheckSessionScript, StringComparison.InvariantCulture)
                        .ReplaceLineEndings("\n");

                    LastCheckSessionCookieName = cookieName;
                }
            }
        }
        return FormattedHtml;
    }

    private static string GetEmbeddedResource(string resourceName)
    {
        var assembly = typeof(CheckSessionHttpWriter).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private const string Html = @"
<!DOCTYPE html>
<!--Copyright (c) Duende Software. All rights reserved.-->
<!--See LICENSE in the project root for license information.-->
<html>
<head>
    <meta http-equiv='X-UA-Compatible' content='IE=edge' />
    <title>Check Session IFrame</title>
</head>
<body>
    <script id='cookie-name' type='application/json'>{cookieName}</script>
    <script>{script}</script>
</body>
</html>
";
}
