// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Tests.TestInfra;
using TestLoggerProvider = Duende.Bff.Tests.TestFramework.TestLoggerProvider;

namespace Duende.Bff.Tests.MultiFrontend;

public class FrontendSelectorTests
{
    private readonly FrontendCollection _frontendCollection =
        new(plugins: [],
            bffConfiguration: TestOptionsMonitor.Create(new BffConfiguration()));

    private readonly FrontendSelector _selector;
    private static readonly TestData The = new();

    private readonly StringBuilder _logMessages = new();

    public FrontendSelectorTests()
    {
        var testLoggerProvider = new TestLoggerProvider((s) =>
            _logMessages.AppendLine(s), "", forceToWriteOutput: true);
        var loggerFactory = new LoggerFactory([testLoggerProvider]);


        _frontendCollection.AddOrUpdate(NeverMatchingFrontEnd());
        _selector = new FrontendSelector(_frontendCollection, loggerFactory.CreateLogger<FrontendSelector>());
    }

    private BffFrontend NeverMatchingFrontEnd() => new BffFrontend
    {
        Name = BffFrontendName.Parse("should not be found"),
        SelectionCriteria = new FrontendSelectionCriteria()
        {
            MatchingOrigin = Origin.Parse("https://will-not-be-found"),
            MatchingPath = "/will_not_be_found",
        }
    };

    [Fact]
    public void TryMapFrontend_EmptyStore_ReturnsFalse()
    {
        // Act
        var result = _selector.TrySelectFrontend(CreateHttpRequest("https://test.com"), out var frontend);

        // Assert
        result.ShouldBeFalse();
        frontend.ShouldBeNull();
    }

    [Fact]
    public void TryMapFrontend_Will_return_first()
    {
        // Arrange
        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("test-frontend1")));
        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("test-frontend2")));

        // Act
        var result = _selector.TrySelectFrontend(CreateHttpRequest("https://test.com"), out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe("test-frontend1");
    }

    [Fact]
    public void TryMapFrontend_MatchesByOrigin_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            origin: Origin.Parse("https://test.com"));
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var result = _selector.TrySelectFrontend(CreateHttpRequest("https://test.com"), out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }

    [Fact]
    public void TryMapFrontend_MatchesByPath_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            path: "/path1");
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var request = CreateHttpRequest("https://test.com/path1/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }

    [Fact]
    public void TryMapFrontend_MatchesByPath_logs_warning_on_invalid_case()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            path: "/lower_case_path");
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var request = CreateHttpRequest("https://test.com/LOWER_CASE_PATH/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);

        _logMessages.ToString().ShouldContain("has different case");
    }

    [Fact]
    public void TryMapFrontend_MatchesByOriginAndPath_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            origin: Origin.Parse("https://test.com"),
            path: "/path1");
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var request = CreateHttpRequest("https://test.com/path1/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }

    [Fact]
    public void TryMapFrontend_NoOriginSpecified_MatchesByPath()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            path: "/path1");
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var request = CreateHttpRequest("https://any-domain.com/path1/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }

    [Fact]
    public void TryMapFrontend_MultipleOrigins_MatchesMostSpecific()
    {
        // Arrange
        var frontendGeneral = CreateFrontend(BffFrontendName.Parse("general-frontend"),
            origin: Origin.Parse("https://test.com"));

        var frontendSpecific = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            origin: Origin.Parse("https://test.com"),
            path: "/path1");

        _frontendCollection.AddOrUpdate(frontendGeneral);
        _frontendCollection.AddOrUpdate(frontendSpecific);

        // Act
        var request = CreateHttpRequest("https://test.com/path1/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe("specific-frontend");
    }

    [Fact]
    public void TryMapFrontend_MultiplePaths_MatchesMostSpecific()
    {
        // Arrange
        var frontendGeneral = CreateFrontend(BffFrontendName.Parse("general-frontend"),
            path: "/path");

        var frontendSpecific = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            path: "/path/subpath");

        _frontendCollection.AddOrUpdate(frontendGeneral);
        _frontendCollection.AddOrUpdate(frontendSpecific);

        // Act
        var request = CreateHttpRequest("https://test.com/path/subpath/detail");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe("specific-frontend");
    }

    [Fact]
    public void TryMapFrontend_NoMatches_ReturnsFalse()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            origin: Origin.Parse("https://test.com"),
            path: "/path1");

        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var request = CreateHttpRequest("https://different.com/different-path");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeFalse();
        selectedFrontend.ShouldBeNull();
    }

    [Fact]
    public void TryMapFrontend_FallbackToDefaultFrontend_ReturnsTrue()
    {
        // Arrange
        var specificFrontend = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            origin: Origin.Parse("https://specific.com"));

        var defaultFrontend = CreateFrontend(BffFrontendName.Parse("default-frontend"));

        _frontendCollection.AddOrUpdate(specificFrontend);
        _frontendCollection.AddOrUpdate(defaultFrontend);

        // Act
        var request = CreateHttpRequest("https://different.com");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe("default-frontend");
    }


    // Helper methods
    private static HttpRequest CreateHttpRequest(string url)
    {
        var uri = new Uri(url);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = uri.Scheme;
        httpContext.Request.Host = new HostString(uri.Host, uri.Port);
        httpContext.Request.Path = uri.AbsolutePath;

        return httpContext.Request;
    }

    private static BffFrontend CreateFrontend(
        BffFrontendName name,
        Origin? origin = null,
        string? path = null
        )
    {
        var selectionCriteria = new FrontendSelectionCriteria
        {
            MatchingOrigin = origin,
            MatchingPath = path,
        };

        return new BffFrontend
        {
            Name = name,
            SelectionCriteria = selectionCriteria
        };
    }
}

