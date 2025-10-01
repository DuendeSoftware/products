using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions.WsFederation;

public class WsFederationProvider : IdentityProvider
{
    public WsFederationProvider() : base(WsFederationProviderConfigurationModel.Type)
    {
    }

    public WsFederationProvider(IdentityProvider other) : base(WsFederationProviderConfigurationModel.Type, other)
    {
    }

    public string? MetadataAddress
    {
        get => this["MetadataAddress"];
        set => this["MetadataAddress"] = value;
    }
    public string? RelyingPartyId
    {
        get => this["RelyingPartyId"];
        set => this["RelyingPartyId"] = value;
    }
    public bool AllowIdpInitiated
    {
        get => this["AllowIdpInitiated"] == "true";
        set => this["AllowIdpInitiated"] = value ? "true" : "false";
    }
}