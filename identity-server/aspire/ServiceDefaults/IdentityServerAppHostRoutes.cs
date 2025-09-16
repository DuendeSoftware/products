// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;

namespace ServiceDefaults;

public class IdentityServerAppHostRoutes : IAppHostServiceRoutes
{
    public string[] ServiceNames => [
        AppHostServices.IdentityServer,
        AppHostServices.Web
    ];

    public Uri UrlTo(string clientName)
    {
        var url = clientName switch
        {
            AppHostServices.IdentityServer => "https://localhost:5001",
            AppHostServices.MvcAutomaticTokenManagement => "https://localhost:44301",
            AppHostServices.MvcCode => "https://localhost:44302",
            AppHostServices.MvcDPoP => "https://localhost:44310",
            AppHostServices.MvcHybridBackChannel => "https://localhost:44303",
            AppHostServices.MvcJarJwt => "https://localhost:44304",
            AppHostServices.MvcJarUriJwt => "https://localhost:44305",
            AppHostServices.Web => "https://localhost:44306",
            _ => throw new InvalidOperationException("client not configured")
        };
        return new Uri(url);
    }
}

public class AppHostServices
{
    public const string IdentityServer = "is-host";
    public const string MvcAutomaticTokenManagement = "mvc-automatic-token-management";
    public const string MvcCode = "mvc-code";
    public const string MvcDPoP = "mvc-dpop";
    public const string MvcHybridBackChannel = "mvc-hybrid-backchannel";
    public const string MvcJarJwt = "mvc-jar-jwt";
    public const string MvcJarUriJwt = "mvc-jar-uri-jwt";
    public const string Web = "web";
}
