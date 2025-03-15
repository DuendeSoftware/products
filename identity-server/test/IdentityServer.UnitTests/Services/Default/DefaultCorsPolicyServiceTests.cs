// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using UnitTests.Common;

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
        (await subject.IsOriginAllowedAsync(null)).ShouldBe(false);
        (await subject.IsOriginAllowedAsync(string.Empty)).ShouldBe(false);
        (await subject.IsOriginAllowedAsync("    ")).ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsAllowed_ReturnsTrue()
    {
        subject.AllowedOrigins.Add("http://foo");
        (await subject.IsOriginAllowedAsync("http://foo")).ShouldBe(true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsNotAllowed_ReturnsFalse()
    {
        subject.AllowedOrigins.Add("http://foo");
        (await subject.IsOriginAllowedAsync("http://bar")).ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsInAllowedList_ReturnsTrue()
    {
        subject.AllowedOrigins.Add("http://foo");
        subject.AllowedOrigins.Add("http://bar");
        subject.AllowedOrigins.Add("http://baz");
        (await subject.IsOriginAllowedAsync("http://bar")).ShouldBe(true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_OriginIsNotInAllowedList_ReturnsFalse()
    {
        subject.AllowedOrigins.Add("http://foo");
        subject.AllowedOrigins.Add("http://bar");
        subject.AllowedOrigins.Add("http://baz");
        (await subject.IsOriginAllowedAsync("http://quux")).ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IsOriginAllowed_AllowAllTrue_ReturnsTrue()
    {
        subject.AllowAll = true;
        (await subject.IsOriginAllowedAsync("http://foo")).ShouldBe(true);
    }
}
