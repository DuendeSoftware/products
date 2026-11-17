// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Models;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlatformUserManagementApp.Pages.Users;

[Authorize]
public sealed class IndexModel(IUserProfileAdmin profileAdmin) : PageModel
{
    private const int PageSize = 10;

    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }
    [BindProperty(SupportsGet = true)] public string? SortField { get; set; }
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public IReadOnlyList<UserProfile> Users { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> Schema { get; set; } =
        new Dictionary<AttributeCode, AttributeDefinition>();

    public string? FilterError { get; set; }

    public async Task OnGetAsync()
    {
        Schema = (await profileAdmin.GetSchemaAsync(HttpContext.RequestAborted)).AttributeDefinitions;

        FilterBy? filterBy = null;
        if (!string.IsNullOrWhiteSpace(Filter))
        {
            try
            {
                var expr = SearchExpression.Create(Filter);
                filterBy = FilterBy.FromSearchExpression(expr);
            }
            catch (Exception)
            {
                FilterError = "Invalid filter expression. Please check the SCIM filter syntax.";
            }
        }

        SortBy? sortBy = null;
        if (!string.IsNullOrWhiteSpace(SortField) &&
            AttributeCode.TryCreate(SortField, out var sortAttr))
        {
            sortBy = SortBy.Attribute(sortAttr.Value);
        }

        var page = Math.Max(1, CurrentPage);
        var range = DataRange.FromPage(page, PageSize);

        var request = filterBy is not null && sortBy is not null
            ? QueryRequest.Create(filterBy, sortBy, range)
            : filterBy is not null
                ? QueryRequest.Create(filterBy, range)
                : sortBy is not null
                    ? QueryRequest.Create(sortBy, range)
                    : QueryRequest.Create(range);

        try
        {
            var result = await profileAdmin.QueryAsync(request, HttpContext.RequestAborted);
            Users = result.Results.ToList();
            TotalCount = result.TotalCount ?? result.Results.Count;
            TotalPages = result.TotalPages ?? (int)Math.Ceiling((double)TotalCount / PageSize);
        }
        catch (Exception ex)
        {
            FilterError ??= $"Query failed: {ex.Message}";
        }
    }
}
