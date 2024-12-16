// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using FluentAssertions;
using System;
using System.Threading.Tasks;
using UnitTests.Common;
using Xunit;

namespace UnitTests.Services.Default;

public class DefaultCorsPolicyServiceTests
{
    private const string Category = "DefaultCorsPolicyService";

    private DefaultCorsPolicyService subject;

    public DefaultCorsPolicyServiceTests()
    {
        subject = new DefaultCorsPolicyService(TestLogger.Create<DefaultCorsPolicyService>());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_null_param_ReturnsFalse()
    {
        (await subject.IsOriginAllowedAsync(null)).Should().Be(false);
        (await subject.IsOriginAllowedAsync(String.Empty)).Should().Be(false);
        (await subject.IsOriginAllowedAsync("    ")).Should().Be(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsAllowed_ReturnsTrue()
    {
        subject.AllowedOrigins.Add("http://foo");
        (await subject.IsOriginAllowedAsync("http://foo")).Should().Be(true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsNotAllowed_ReturnsFalse()
    {
        subject.AllowedOrigins.Add("http://foo");
        (await subject.IsOriginAllowedAsync("http://bar")).Should().Be(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsInAllowedList_ReturnsTrue()
    {
        subject.AllowedOrigins.Add("http://foo");
        subject.AllowedOrigins.Add("http://bar");
        subject.AllowedOrigins.Add("http://baz");
        (await subject.IsOriginAllowedAsync("http://bar")).Should().Be(true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsNotInAllowedList_ReturnsFalse()
    {
        subject.AllowedOrigins.Add("http://foo");
        subject.AllowedOrigins.Add("http://bar");
        subject.AllowedOrigins.Add("http://baz");
        (await subject.IsOriginAllowedAsync("http://quux")).Should().Be(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_AllowAllTrue_ReturnsTrue()
    {
        subject.AllowAll = true;
        (await subject.IsOriginAllowedAsync("http://foo")).Should().Be(true);
    }
}
