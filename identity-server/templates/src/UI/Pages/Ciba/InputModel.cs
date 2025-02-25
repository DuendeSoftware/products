// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace IdentityServerHost.Pages.Ciba;

public class InputModel
{
    public string? Button { get; set; }
    public IEnumerable<string> ScopesConsented { get; set; } = new List<string>();
    public string? Id { get; set; }
    public string? Description { get; set; }
}