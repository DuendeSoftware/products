using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Components.Database;

public class DatabaseViewComponent(
    PersistedGrantDbContext persistedGrants,
    ConfigurationDbContext configuration) : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        var operationalDatabase = persistedGrants.Database.ProviderName?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Unknown";
        var configurationDatabase = configuration.Database.ProviderName?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Unknown";
        var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "Unknown";

        var vm = new DatabaseViewModel
        {
            OperationalDatabaseKind = operationalDatabase,
            ConfigurationDatabaseKind = configurationDatabase,
            EntityFrameworkCoreVersion = efVersion
        };

        return Task.FromResult<IViewComponentResult>(View(vm));
    }
}

public class DatabaseViewModel
{
    public string OperationalDatabaseKind { get; init; } = "";
    public string ConfigurationDatabaseKind { get; init; } = "";
    public string EntityFrameworkCoreVersion { get; init; } = "";
}
