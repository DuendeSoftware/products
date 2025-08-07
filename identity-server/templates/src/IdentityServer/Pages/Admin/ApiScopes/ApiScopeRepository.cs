using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerTemplate.Pages.Admin.ApiScopes;

public class ApiScopeSummaryModel
{
    [Required]
    [DisplayName("Name")]
    public string Name { get; set; } = default!;
    [DisplayName("Display Name")]
    public string? DisplayName { get; set; }
}

public class ApiScopeModel : ApiScopeSummaryModel
{
    [DisplayName("User Claims")]
    public string[] UserClaims { get; set; } = [];

    public string[] ClaimsSuggestions { get; set; } = [];
}

public class ApiScopeRepository(ConfigurationDbContext context)
{
    private readonly ConfigurationDbContext _context = context;

    public async Task<IEnumerable<ApiScopeSummaryModel>> GetAllAsync(string? filter = null)
    {
        var query = _context.ApiScopes
            .Include(x => x.UserClaims)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => x.Name.Contains(filter) || x.DisplayName.Contains(filter));
        }

        var result = query.Select(x => new ApiScopeSummaryModel
        {
            Name = x.Name,
            DisplayName = x.DisplayName
        });

        return await result.ToArrayAsync();
    }

    public async Task<ApiScopeModel?> GetByIdAsync(string id)
    {
        var scope = await _context.ApiScopes
            .Include(x => x.UserClaims)
            .SingleOrDefaultAsync(x => x.Name == id);

        if (scope == null)
        {
            return null;
        }

        // Define a list of well-known user claims
        string[] wellknownClaims = [
            "role",
            "scope",
            "email",
            "name",
            "given_name",
            "family_name",
            "middle_name",
            "nickname",
            "preferred_username",
            "profile",
            "picture",
            "website"
        ];

        // Combine the user claims with the well-known user claims
        var combinedClaims = scope.UserClaims
            .Select(x => x.Type)
            .Concat(wellknownClaims)
            .Distinct()
            .ToArray();

        // Return the model
        return new ApiScopeModel
        {
            Name = scope.Name,
            DisplayName = scope.DisplayName,
            UserClaims = scope.UserClaims.Count != 0 ? [.. scope.UserClaims.Select(x => x.Type)] : [],
            ClaimsSuggestions = combinedClaims
        };
    }

    public async Task CreateAsync(ApiScopeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var exists = await _context.ApiScopes.AnyAsync(x => x.Name == model.Name);
        if (exists)
        {
            throw new ValidationException($"An API Scope with the name '{model.Name}' already exists.");
        }
        var scope = new Duende.IdentityServer.Models.ApiScope()
        {
            Name = model.Name,
            DisplayName = model.DisplayName?.Trim()
        };

        var claims = model.UserClaims?.Where(uc => !string.IsNullOrWhiteSpace(uc)) ?? [];
        if (claims.Any())
        {
            scope.UserClaims = [.. claims];
        }

#pragma warning disable CA1849 // Call async methods when in an async method
        // CA1849 Suppressed because AddAsync is only needed for value generators that
        // need async database access (e.g., HiLoValueGenerator), and we don't use those
        // generators
        _context.ApiScopes.Add(scope.ToEntity());
#pragma warning restore CA1849 // Call async methods when in an async method
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ApiScopeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var scope = await _context.ApiScopes
            .Include(x => x.UserClaims)
            .SingleOrDefaultAsync(x => x.Name == model.Name)
            ?? throw new ArgumentException("Invalid Api Scope");

        if (scope.DisplayName != model.DisplayName)
        {
            scope.DisplayName = model.DisplayName?.Trim();
        }

        var claims = model.UserClaims ?? Enumerable.Empty<string>();
        var currentClaims = (scope.UserClaims.Select(x => x.Type) ?? []).ToArray();

        var claimsToAdd = claims.Except(currentClaims).Where(uc => !string.IsNullOrWhiteSpace(uc)).ToArray();
        var claimsToRemove = currentClaims.Except(claims).ToArray();

        if (claimsToRemove.Length != 0)
        {
            scope.UserClaims.RemoveAll(x => claimsToRemove.Contains(x.Type));
        }
        if (claimsToAdd.Length != 0)
        {
            scope.UserClaims.AddRange(claimsToAdd.Select(x => new ApiScopeClaim
            {
                Type = x,
            }));
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var scope = await _context.ApiScopes.SingleOrDefaultAsync(x => x.Name == id)
            ?? throw new ArgumentException("Invalid Api Scope");

        _context.ApiScopes.Remove(scope);
        await _context.SaveChangesAsync();
    }

}
