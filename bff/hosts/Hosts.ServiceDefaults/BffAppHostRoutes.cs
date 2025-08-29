// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Xunit.Playwright;

namespace Hosts.ServiceDefaults;

public class BffAppHostRoutes : IAppHostServiceRoutes
{
    public string[] ServiceNames => AppHostServices.All;

    public Uri UrlTo(string clientName)
    {
        var url = clientName switch
        {
            AppHostServices.Bff => "https://localhost:5002",
            AppHostServices.BffBlazorPerComponent => "https://localhost:5105",
            AppHostServices.BffMultiFrontend => "https://localhost:5005",
            AppHostServices.BffBlazorWebassembly => "https://localhost:5006",
            AppHostServices.TemplateBffBlazor => "https://localhost:7035",
            _ => throw new InvalidOperationException("client not configured")
        };
        return new Uri(url);
    }
}
