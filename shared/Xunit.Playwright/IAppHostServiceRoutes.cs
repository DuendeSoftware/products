// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Xunit.Playwright;

/// <summary>
/// This interface describes all the services in an AppHost and provides Urls to services in the host for external code.
/// </summary>
public interface IAppHostServiceRoutes
{
    public string[] ServiceNames { get; }
    public Uri UrlTo(string clientName);
}
