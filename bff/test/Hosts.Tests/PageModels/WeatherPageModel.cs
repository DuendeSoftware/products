using Microsoft.Playwright;

namespace Hosts.Tests.PageModels;

public class WeatherPageModel : WebAssemblyPageModel
{
    public async Task VerifyWeatherListIsShown()
    {
        // Verify that the list is actually loading
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = "Summary" })).ToBeVisibleAsync();
    }
}