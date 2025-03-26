// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace IdentityServerHost.Pages.Grants;

public class ViewModel
{
    public IEnumerable<GrantViewModel> Grants { get; set; } = Enumerable.Empty<GrantViewModel>();
}

public class GrantViewModel
{
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientUrl { get; set; }
    public string? ClientLogoUrl { get; set; }
    public string? Description { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Expires { get; set; }
    public IEnumerable<string> IdentityGrantNames { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> ApiGrantNames { get; set; } = Enumerable.Empty<string>();
}
