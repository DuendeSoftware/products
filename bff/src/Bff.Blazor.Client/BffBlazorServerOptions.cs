// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Blazor.Client;

/// <summary>
/// Options for Blazor BFF on the server. 
/// </summary>
public class BffBlazorServerOptions
{
    /// <summary>
    /// The delay, in milliseconds, between polling requests by the
    /// BffServerAuthenticationStateProvider to the /bff/user endpoint. Defaults to 5000
    /// ms.
    /// </summary>
    public int ServerStateProviderPollingInterval { get; set; } = 5000;
}
