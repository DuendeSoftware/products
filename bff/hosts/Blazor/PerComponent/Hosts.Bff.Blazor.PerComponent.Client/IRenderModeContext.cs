// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.Bff.Blazor.PerComponent.Client;

public interface IRenderModeContext
{
    RenderMode GetMode();
    string WhereAmI() => GetMode() switch
    {
        RenderMode.Server => "Server (streamed over circuit)",
        RenderMode.Client => "Client (wasm)",
        RenderMode.Prerender => "Prerender (single response)",
        _ => throw new ArgumentException(),
    };
}

public enum RenderMode
{
    Server, Client, Prerender
}