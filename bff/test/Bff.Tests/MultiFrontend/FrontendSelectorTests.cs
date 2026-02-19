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
    private readonly FrontendCollection _frontendCollection;

    private readonly FrontendSelector _selector;
    private static readonly TestData The = new();
    internal TestDataBuilder Some => new(The);
    private readonly StringBuilder _logMessages = new();
    private readonly ITestOutputHelper _output = TestContext.Current.TestOutputHelper!;

    public FrontendSelectorTests()
    {
        _frontendCollection = new(plugins: [],
            bffConfiguration: TestOptionsMonitor.Create(new BffConfiguration()),
            licenseValidator: Some.LicenseValidator
        );
        var testLoggerProvider = new TestLoggerProvider((s) =>
        {
            _output.WriteLine(s);
            _logMessages.AppendLine(s);
        }, "", forceToWriteOutput: true);
        var loggerFactory = new LoggerFactory([testLoggerProvider]);


        _frontendCollection.AddOrUpdate(NeverMatchingFrontEnd());
        _selector = new FrontendSelector(_frontendCollection, loggerFactory.CreateLogger<FrontendSelector>());
    }

    private BffFrontend NeverMatchingFrontEnd() => new BffFrontend
    {
        Name = BffFrontendName.Parse("should_not_be_found"),
        MatchingCriteria = new FrontendMatchingCriteria()
        {
            MatchingHostHeader = HostHeaderValue.Parse("https://will-not-be-found"),
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
        var httpRequest = CreateHttpRequest("https://test.com");
        var result = _selector.TrySelectFrontend(httpRequest, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe("test-frontend1");
    }

    [Fact]
    public void TryMapFrontend_MatchesByHost_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            host: HostHeaderValue.Parse("https://test.com"));
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var httpRequest = CreateHttpRequest("https://test.com");
        var result = _selector.TrySelectFrontend(httpRequest, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }
    [Fact]
    public void TryMapFrontend_MatchesByHostUri_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            uri: new Uri("https://test.com"));
        _frontendCollection.AddOrUpdate(frontend);

        // Act
        var httpRequest = CreateHttpRequest("https://test.com");
        var result = _selector.TrySelectFrontend(httpRequest, out var selectedFrontend);

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

        //_logMessages.ToString().ShouldContain("has different case");
    }

    [Fact]
    public void TryMapFrontend_MatchesByHostAndPath_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            host: HostHeaderValue.Parse("https://test.com"),
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
    public void TryMapFrontend_MatchesByHostAndPathUri_ReturnsTrue()
    {
        // Arrange
        var frontend = CreateFrontend(The.FrontendName,
            uri: new Uri("https://test.com/path1"));
        _frontendCollection.AddOrUpdate(frontend);
        _frontendCollection.AddOrUpdate(CreateNonMatching().MapTo(new Uri("https://test.com/")));
        _frontendCollection.AddOrUpdate(CreateNonMatching().MapToPath("/path1"));

        // Act
        var request = CreateHttpRequest("https://test.com/path1/subpath");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);

        // Assert
        result.ShouldBeTrue();
        selectedFrontend.ShouldNotBeNull();
        selectedFrontend.Name.ToString().ShouldBe(The.FrontendName);
    }


    [Fact]
    public void TryMapFrontend_NoHostSpecified_MatchesByPath()
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
    public void TryMapFrontend_MultipleHosts_MatchesMostSpecific()
    {
        // Arrange
        var frontendGeneral = CreateFrontend(BffFrontendName.Parse("general-frontend"),
            host: HostHeaderValue.Parse("https://test.com"));

        var frontendSpecific = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            host: HostHeaderValue.Parse("https://test.com"),
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
            host: HostHeaderValue.Parse("https://test.com"),
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
    public void HostHeader_takes_precedence_over_path()
    {
        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("path_and_Host"),
            host: HostHeaderValue.Parse("https://test.com"),
            path: "/path"));
        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("path_subpath_and_Host"),
            host: HostHeaderValue.Parse("https://test.com"),
            path: "/path/subpath"));

        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("Host"),
            host: HostHeaderValue.Parse("https://test.com")));

        _frontendCollection.AddOrUpdate(CreateFrontend(BffFrontendName.Parse("path"),
            path: "/path"));


        // Act
        _selector.TrySelectFrontend(CreateHttpRequest("https://different.com/path"), out var selectedFrontend);
        selectedFrontend!.Name.ToString().ShouldBe("path");

        _selector.TrySelectFrontend(CreateHttpRequest("https://test.com/otherpath"), out selectedFrontend);
        selectedFrontend!.Name.ToString().ShouldBe("Host");

        _selector.TrySelectFrontend(CreateHttpRequest("https://test.com/path/otherSubPath"), out selectedFrontend);
        selectedFrontend!.Name.ToString().ShouldBe("path_and_Host");

        _selector.TrySelectFrontend(CreateHttpRequest("https://test.com/path/subpath"), out selectedFrontend);
        selectedFrontend!.Name.ToString().ShouldBe("path_subpath_and_Host");
    }

    [Fact]
    public void Slash_also_functions_as_default_frontend()
    {
        // Arrange
        var specificFrontend = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            host: HostHeaderValue.Parse("https://specific.com"));

        var defaultFrontend = CreateFrontend(BffFrontendName.Parse("default-frontend")).MapToPath("/");

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

    [Fact]
    public void TryMapFrontend_FallbackToDefaultFrontend_ReturnsTrue()
    {
        // Arrange
        var specificFrontend = CreateFrontend(BffFrontendName.Parse("specific-frontend"),
            host: HostHeaderValue.Parse("https://specific.com"));

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

    [Theory]
    [InlineData(null, null)]
    [InlineData("/", null)]
    [InlineData("/some_path", null)]
    [InlineData(null, "https://some-url")]
    [InlineData("/some_path", "https://some-url")]
    public void When_mapping_duplicate_then_warning_is_written(string? path, string? uri)
    {
        var f1 = CreateFrontend(BffFrontendName.Parse("f1"));

        var f2 = CreateFrontend(BffFrontendName.Parse("f2"));

        if (path != null && uri != null)
        {
            // Also, test that the combined map equals the same as individual ones
            // Also, verify that it's case insensitive
            f1 = f1.MapTo(HostHeaderValue.Parse(new Uri(uri)), path);
            f2 = f2.MapTo(new Uri(new Uri(uri.ToUpper()), path.ToUpper()));
        }
        else if (path != null)
        {
            f1 = f1.MapToPath(path);
            f2 = f2.MapToPath(path.ToUpper());
        }
        else if (uri != null)
        {
            f1 = f1.MapTo(new Uri(uri));
            f2 = f2.MapTo(new Uri(uri.ToUpper()));
        }
        _frontendCollection.AddOrUpdate(f1);
        _frontendCollection.AddOrUpdate(f2);

        // Act
        var request = CreateHttpRequest("https://some-url/some_path");
        var result = _selector.TrySelectFrontend(request, out var selectedFrontend);
        result.ShouldBe(true);
        selectedFrontend!.Name.ShouldBe(f1.Name);
        var expectedWarning = "Duplicate Frontend matching criteria registered. Frontend 'f2'";

        _logMessages.ToString().ShouldContain(expectedWarning);

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
        HostHeaderValue? host = null,
        string? path = null
        )
    {
        var frontendMatchingCriteria = new FrontendMatchingCriteria
        {
            MatchingHostHeader = host,
            MatchingPath = path,
        };

        return new BffFrontend
        {
            Name = name,
            MatchingCriteria = frontendMatchingCriteria
        };
    }

    private int seed = 0;
    private BffFrontend CreateNonMatching() => new BffFrontend(BffFrontendName.Parse("wrong" + seed++));

    private static BffFrontend CreateFrontend(
        BffFrontendName name,
        Uri uri
    ) => new BffFrontend
    {
        Name = name,
    }.MapTo(uri);
}

