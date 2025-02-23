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

    protected virtual Task Verify()
    {
        return Task.CompletedTask;
    }

    public ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

}