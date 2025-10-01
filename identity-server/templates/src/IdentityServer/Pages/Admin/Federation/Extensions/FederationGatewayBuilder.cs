namespace IdentityServerTemplate.Pages.Admin.Federation.Extensions;

public class FederationGatewayBuilder : IFederationGatewayBuilder
{
    public FederationGatewayBuilder(IServiceCollection services) => Services = services ?? throw new ArgumentNullException(nameof(services));
    public IServiceCollection Services { get; }
}
