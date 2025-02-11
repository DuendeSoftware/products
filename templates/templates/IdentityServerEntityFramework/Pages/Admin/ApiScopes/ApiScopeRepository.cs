// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace IdentityServerHost.Pages.Admin.ApiScopes;

public class ApiScopeSummaryModel
{
    [Required]
    public string Name { get; set; } = default!;
    public string? DisplayName { get; set; }
}

public class ApiScopeModel : ApiScopeSummaryModel
{
    public string? UserClaims { get; set; } = default!;
}


public class ApiScopeRepository
{
    private readonly ConfigurationDbContext _context;

    public ApiScopeRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ApiScopeSummaryModel>> GetAllAsync(string? filter = null)
    {
        var query = _context.ApiScopes
            .Include(x => x.UserClaims)
            .AsQueryable();

        if (!String.IsNullOrWhiteSpace(filter))
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

        if (scope == null) return null;

        return new ApiScopeModel
        {
            Name = scope.Name,
            DisplayName = scope.DisplayName,
            UserClaims = scope.UserClaims.Count != 0 ? scope.UserClaims.Select(x => x.Type).Aggregate((a, b) => $"{a} {b}") : null,
        };
    }

    public async Task CreateAsync(ApiScopeModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var scope = new Duende.IdentityServer.Models.ApiScope()
        {
            Name = model.Name,
            DisplayName = model.DisplayName?.Trim()
        };

        var claims = model.UserClaims?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray() ?? Enumerable.Empty<string>();
        if (claims.Any())
        {
            scope.UserClaims = claims.ToList();
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
            .SingleOrDefaultAsync(x => x.Name == model.Name);

        if (scope == null) throw new ArgumentException("Invalid Api Scope");

        if (scope.DisplayName != model.DisplayName)
        {
            scope.DisplayName = model.DisplayName?.Trim();
        }

        var claims = model.UserClaims?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray() ?? Enumerable.Empty<string>();
        var currentClaims = (scope.UserClaims.Select(x => x.Type) ?? Enumerable.Empty<String>()).ToArray();

        var claimsToAdd = claims.Except(currentClaims).ToArray();
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
        var scope = await _context.ApiScopes.SingleOrDefaultAsync(x => x.Name == id);

        if (scope == null) throw new ArgumentException("Invalid Api Scope");

        _context.ApiScopes.Remove(scope);
        await _context.SaveChangesAsync();
    }


}
