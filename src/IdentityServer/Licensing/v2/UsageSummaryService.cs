using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// 
/// </summary>
public class UsageSummaryService(ILicenseAccessor license, ILicenseUsageService licenseUsage, ILogger<UsageSummaryService> logger) : IHostedService
{
    /// <summary>
    /// 
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (!license.Current.IsConfigured)
        {
            var features = licenseUsage.UsedFeatures.ToArray();
            var clients = licenseUsage.UsedClients.ToArray();
            var issuers = licenseUsage.UsedIssuers.ToArray();
            logger.LogInformation(message: "Thank you for trying IdentityServer! A license is required in production. To help you choose the right license, here is a summary of your usage of IdentityServer. {clientCount} Client(s): {clients}. Features: {features}. {issuerCount} Issuer(s): {issuers}",
                clients.Length,
                string.Join(',', clients),
                string.Join(',', features),
                issuers.Length,
                string.Join(',', issuers));
        }
        return Task.CompletedTask;
    }
}
