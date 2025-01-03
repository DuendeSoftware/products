using NSubstitute;
using Shouldly;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class AntiforgeryHandlerTests
{
    [Fact]
    public async Task Adds_expected_header()
    {
        var sut = new TestAntiforgeryHandler()
        {
            InnerHandler = Substitute.For<HttpMessageHandler>()
        };

        var request = new HttpRequestMessage();

        await sut.SendAsync(request, CancellationToken.None);

        request.Headers.ShouldContain(h => h.Key == "X-CSRF" && h.Value.Contains("1"));
    }
}

public class TestAntiforgeryHandler : AntiforgeryHandler
{
    public new Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return base.SendAsync(request, cancellationToken);
    }
}