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