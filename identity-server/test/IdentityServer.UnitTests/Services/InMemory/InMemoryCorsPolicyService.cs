// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using UnitTests.Common;

namespace UnitTests.Services.InMemory;

public class InMemoryCorsPolicyServiceTests
{
    private const string Category = "InMemoryCorsPolicyService";

    private InMemoryCorsPolicyService _subject;
    private List<Client> _clients = new List<Client>();

    public InMemoryCorsPolicyServiceTests() => _subject = new InMemoryCorsPolicyService(TestLogger.Create<InMemoryCorsPolicyService>(), _clients);

    [Fact]
    [Trait("Category", Category)]
    public async Task client_has_origin_should_allow_origin()
    {
        _clients.Add(new Client
        {
            AllowedCorsOrigins = new List<string>
            {
                "http://foo"
            }
        });

        var result = await _subject.IsOriginAllowedAsync("http://foo");
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("http://foo")]
    [InlineData("https://bar")]
    [InlineData("http://bar-baz")]
    [Trait("Category", Category)]
    public async Task client_does_not_has_origin_should_not_allow_origin(string clientOrigin)
    {
        _clients.Add(new Client
        {
            AllowedCorsOrigins = new List<string>
            {
                clientOrigin
            }
        });
        var result = await _subject.IsOriginAllowedAsync("http://bar");
        result.ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_has_many_origins_and_origin_is_in_list_should_allow_origin()
    {
        _clients.Add(new Client
        {
            AllowedCorsOrigins = new List<string>
            {
                "http://foo",
                "http://bar",
                "http://baz"
            }
        });
        var result = await _subject.IsOriginAllowedAsync("http://bar");
        result.ShouldBe(true);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task client_has_many_origins_and_origin_is_in_not_list_should_not_allow_originAsync()
    {
        _clients.Add(new Client
        {
            AllowedCorsOrigins = new List<string>
            {
                "http://foo",
                "http://bar",
                "http://baz"
            }
        });
        var result = await _subject.IsOriginAllowedAsync("http://quux");
        result.ShouldBe(false);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task many_clients_have_same_origins_should_allow_originAsync()
    {
        _clients.AddRange(new Client[] {
            new Client
            {
                AllowedCorsOrigins = new List<string>
                {
                    "http://foo"
                }
            },
            new Client
            {
                AllowedCorsOrigins = new List<string>
                {
                    "http://foo"
                }
            }
        });
        var result = await _subject.IsOriginAllowedAsync("http://foo");
        result.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task handle_invalid_cors_origin_format_exceptionAsync()
    {
        _clients.AddRange(new Client[] {
            new Client
            {
                AllowedCorsOrigins = new List<string>
                {
                    "http://foo",
                    "http://ba z"
                }
            },
            new Client
            {
                AllowedCorsOrigins = new List<string>
                {
                    "http://foo",
                    "http://bar"
                }
            }
        });
        var result = await _subject.IsOriginAllowedAsync("http://bar");
        result.ShouldBeTrue();
    }
}
