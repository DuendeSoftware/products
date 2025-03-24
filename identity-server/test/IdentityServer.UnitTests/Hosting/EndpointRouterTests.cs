// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Hosting;

public class EndpointRouterTests
{
    private Dictionary<string, Duende.IdentityServer.Hosting.Endpoint> _pathMap;
    private readonly List<Duende.IdentityServer.Hosting.Endpoint> _endpoints;
    private readonly IdentityServerOptions _options;
    private readonly EndpointRouter _subject;

    public EndpointRouterTests()
    {
        _pathMap = new Dictionary<string, Duende.IdentityServer.Hosting.Endpoint>();
        _endpoints = new List<Duende.IdentityServer.Hosting.Endpoint>();
        _options = new IdentityServerOptions();
        var licenseAccessor = new LicenseAccessor(new IdentityServerOptions(), NullLogger<LicenseAccessor>.Instance);
        var licenseExpirationChecker = new LicenseExpirationChecker(licenseAccessor, new MockSystemClock(), new NullLoggerFactory());
        var protocolRequestCounter = new ProtocolRequestCounter(licenseAccessor, new NullLoggerFactory());
        _subject = new EndpointRouter(_endpoints, protocolRequestCounter, licenseExpirationChecker, _options, new SanitizedLogger<EndpointRouter>(TestLogger.Create<EndpointRouter>()));
    }

    [Fact]
    public void Endpoint_ctor_requires_path_to_start_with_slash()
    {
        Action a = () => new Duende.IdentityServer.Hosting.Endpoint("ep1", "ep1", typeof(MyEndpointHandler));
        a.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Find_should_return_null_for_incorrect_path()
    {
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep1", "/ep1", typeof(MyEndpointHandler)));
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep2", "/ep2", typeof(MyOtherEndpointHandler)));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString("/wrong");
        ctx.RequestServices = new StubServiceProvider();

        var result = _subject.Find(ctx);
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_should_find_path()
    {
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep1", "/ep1", typeof(MyEndpointHandler)));
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep2", "/ep2", typeof(MyOtherEndpointHandler)));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString("/ep1");
        ctx.RequestServices = new StubServiceProvider();

        var result = _subject.Find(ctx);
        result.ShouldBeOfType<MyEndpointHandler>();
    }

    [Fact]
    public void Find_should_not_find_nested_paths()
    {
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep1", "/ep1", typeof(MyEndpointHandler)));
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep2", "/ep2", typeof(MyOtherEndpointHandler)));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString("/ep1/subpath");
        ctx.RequestServices = new StubServiceProvider();

        var result = _subject.Find(ctx);
        result.ShouldBeNull();
    }

    [Fact]
    public void Find_should_find_first_registered_mapping()
    {
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep1", "/ep1", typeof(MyEndpointHandler)));
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep1", "/ep1", typeof(MyOtherEndpointHandler)));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString("/ep1");
        ctx.RequestServices = new StubServiceProvider();

        var result = _subject.Find(ctx);
        result.ShouldBeOfType<MyEndpointHandler>();
    }

    [Fact]
    public void Find_should_return_null_for_disabled_endpoint()
    {
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint(IdentityServerConstants.EndpointNames.Authorize, "/ep1", typeof(MyEndpointHandler)));
        _endpoints.Add(new Duende.IdentityServer.Hosting.Endpoint("ep2", "/ep2", typeof(MyOtherEndpointHandler)));

        _options.Endpoints.EnableAuthorizeEndpoint = false;

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = new PathString("/ep1");
        ctx.RequestServices = new StubServiceProvider();

        var result = _subject.Find(ctx);
        result.ShouldBeNull();
    }

    private class MyEndpointHandler : IEndpointHandler
    {
        public Task<IEndpointResult> ProcessAsync(HttpContext context)
        {
            throw new NotImplementedException();
        }
    }

    private class MyOtherEndpointHandler : IEndpointHandler
    {
        public Task<IEndpointResult> ProcessAsync(HttpContext context)
        {
            throw new NotImplementedException();
        }
    }

    private class StubServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(MyEndpointHandler)) return new MyEndpointHandler();
            if (serviceType == typeof(MyOtherEndpointHandler)) return new MyOtherEndpointHandler();

            throw new InvalidOperationException();
        }
    }
}
