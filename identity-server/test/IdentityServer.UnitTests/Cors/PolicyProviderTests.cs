// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Services;
using Shouldly;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Cors;

public class PolicyProviderTests
{
    private const string Category = "PolicyProvider";

    private CorsPolicyProvider _subject;
    private List<string> _allowedPaths = new List<string>();

    private MockCorsPolicyProvider _mockInner = new MockCorsPolicyProvider();
    private MockCorsPolicyService _mockPolicy = new MockCorsPolicyService();
    private IdentityServerOptions _options;

    public PolicyProviderTests()
    {
        Init();
    }

    internal void Init()
    {
        _options = new IdentityServerOptions();
        _options.Cors.CorsPaths.Clear();
        foreach (var path in _allowedPaths)
        {
            _options.Cors.CorsPaths.Add(new PathString(path));
        }

        var svcs = new ServiceCollection();
        svcs.AddSingleton<ICorsPolicyService>(_mockPolicy);
        var provider = svcs.BuildServiceProvider();


        _subject = new CorsPolicyProvider(
            TestLogger.Create<CorsPolicyProvider>(),
            new Decorator<ICorsPolicyProvider>(_mockInner),
            _options, provider);
    }

    [Theory]
    [InlineData("/foo")]
    [InlineData("/bar/")]
    [InlineData("/baz/quux")]
    [InlineData("/baz/quux/")]
    [Trait("Category", Category)]
    public async Task valid_paths_should_call_policy_service(string path)
    {
        _allowedPaths.AddRange(new string[] {
            "/foo",
            "/bar/",
            "/baz/quux",
            "/baz/quux/"
        });
        Init();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("server");
        ctx.Request.Path = new PathString(path);
        ctx.Request.Headers.Append("Origin", "http://notserver");

        var response = await _subject.GetPolicyAsync(ctx, _options.Cors.CorsPolicyName);

        _mockPolicy.WasCalled.ShouldBeTrue();
        _mockInner.WasCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("/foo/")]
    [InlineData("/xoxo")]
    [InlineData("/xoxo/")]
    [InlineData("/foo/xoxo")]
    [InlineData("/baz/quux/xoxo")]
    [Trait("Category", Category)]
    public async Task invalid_paths_should_not_call_policy_service(string path)
    {
        _allowedPaths.AddRange(new string[] {
            "/foo",
            "/bar",
            "/baz/quux"
        });
        Init();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("server");
        ctx.Request.Path = new PathString(path);
        ctx.Request.Headers.Append("Origin", "http://notserver");

        var response = await _subject.GetPolicyAsync(ctx, _options.Cors.CorsPolicyName);

        _mockPolicy.WasCalled.ShouldBeFalse();
        _mockInner.WasCalled.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task different_policy_name_should_call_inner_policy_service()
    {
        _allowedPaths.AddRange(new string[] {
            "/foo",
            "/bar",
            "/baz/quux"
        });
        Init();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("server");
        ctx.Request.Path = new PathString("/foo");
        ctx.Request.Headers.Append("Origin", "http://notserver");

        var response = await _subject.GetPolicyAsync(ctx, "wrong_name");

        _mockPolicy.WasCalled.ShouldBeFalse();
        _mockInner.WasCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task origin_same_as_server_should_not_call_policy()
    {
        _allowedPaths.AddRange(new string[] {
            "/foo"
        });
        Init();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("server");
        ctx.Request.Path = new PathString("/foo");
        ctx.Request.Headers.Append("Origin", "https://server");

        var response = await _subject.GetPolicyAsync(ctx, _options.Cors.CorsPolicyName);

        _mockPolicy.WasCalled.ShouldBeFalse();
        _mockInner.WasCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("https://notserver")]
    [InlineData("http://server")]
    [Trait("Category", Category)]
    public async Task origin_not_same_as_server_should_call_policy(string origin)
    {
        _allowedPaths.AddRange(new string[] {
            "/foo"
        });
        Init();

        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = "https";
        ctx.Request.Host = new HostString("server");
        ctx.Request.Path = new PathString("/foo");
        ctx.Request.Headers.Append("Origin", origin);

        var response = await _subject.GetPolicyAsync(ctx, _options.Cors.CorsPolicyName);

        _mockPolicy.WasCalled.ShouldBeTrue();
        _mockInner.WasCalled.ShouldBeFalse();
    }
}