// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Duende.IdentityServer.UI.DevPortal.Pages;

public sealed class IndexModel : PageModel
{
    public string RenderedHtml { get; }
    public IReadOnlyList<ScenarioLink> Links { get; }

    public IndexModel(string renderedHtml, IReadOnlyList<ScenarioLink> links)
    {
        RenderedHtml = renderedHtml;
        Links = links;
    }

    public void OnGet()
    {
    }
}
