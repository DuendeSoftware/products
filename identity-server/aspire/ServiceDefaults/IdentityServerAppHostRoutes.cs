// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;

namespace ServiceDefaults;

public class IdentityServerAppHostRoutes : IAppHostServiceRoutes
{
    public string[] ServiceNames => [
        AppHostServices.IdentityServer,
        AppHostServices.MvcJarUriJwt,
        AppHostServices.Web,
        AppHostServices.TemplateIs,
        AppHostServices.TemplateIsEmpty,
        AppHostServices.TemplateIsInMem,
        AppHostServices.TemplateIsAspid,
        AppHostServices.TemplateIsEF
    ];

    public Uri UrlTo(string clientName)
    {
        var url = clientName switch
        {
            AppHostServices.IdentityServer => "https://localhost:5001",
            AppHostServices.MvcJarUriJwt => "https://localhost:44305",
            AppHostServices.Web => "https://localhost:44306",
            AppHostServices.TemplateIs => "https://localhost:7001",
            AppHostServices.TemplateIsEmpty => "https://localhost:7002",
            AppHostServices.TemplateIsInMem => "https://localhost:7003",
            AppHostServices.TemplateIsAspid => "https://localhost:7004",
            AppHostServices.TemplateIsEF => "https://localhost:7005",
            _ => throw new InvalidOperationException("client not configured")
        };
        return new Uri(url);
    }
}

public class AppHostServices
{
    public const string IdentityServer = "is-host";
    public const string MvcJarUriJwt = "mvc-jar-uri-jwt";
    public const string Web = "web";
    public const string TemplateIs = "template-is";
    public const string TemplateIsEmpty = "template-is-empty";
    public const string TemplateIsInMem = "template-is-inmem";
    public const string TemplateIsAspid = "template-is-aspid";
    public const string TemplateIsEF = "template-is-ef";
}
