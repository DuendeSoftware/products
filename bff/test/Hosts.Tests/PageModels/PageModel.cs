// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class PageModel
{
    public IPage Page { get; init; } = null!;

    public async Task<TPageModel> Build<TPageModel>() where TPageModel : PageModel, new()
    {
        var model = new TPageModel()
        {
            Page = Page
        };

        await model.Verify();
        return model;

    }

    protected virtual Task Verify() => Task.CompletedTask;

    public ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

}
