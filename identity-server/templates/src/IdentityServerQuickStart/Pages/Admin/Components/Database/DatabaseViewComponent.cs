using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerQuickStart.Pages.Components.Database;

public class DatabaseViewComponent(
    PersistedGrantDbContext persistedGrants,
    ConfigurationDbContext configuration) : ViewComponent
{
    public Task<IViewComponentResult> InvokeAsync()
    {
        var persistedGrantDatabase = persistedGrants.Database.ProviderName?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Unknown";
        var configurationDatabase = configuration.Database.ProviderName?.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "Unknown";

        return Task.FromResult<IViewComponentResult>(View(new DatabaseViewModel(persistedGrantDatabase, configurationDatabase)));
    }
}

public record DatabaseViewModel(string PersistedGrantDatabase, string ConfigurationDatabase)
{
    public bool IsSameDatabase => PersistedGrantDatabase == ConfigurationDatabase;
}
