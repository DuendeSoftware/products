// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Xunit;
using Duende.IdentityServer.Extensions;
using Shouldly;
using System.Linq;

namespace UnitTests.Extensions;

public class StringExtensionsTests
{
    private const string Category = "StringExtensions Tests";

    private void CheckOrigin(string inputUrl, string expectedOrigin)
    {
        var actualOrigin = inputUrl.GetOrigin();
        Assert.Equal(expectedOrigin, actualOrigin);
    }

    [Fact]
    public void TestGetOrigin()
    {
        CheckOrigin("http://idsvr.com", "http://idsvr.com");
        CheckOrigin("http://idsvr.com/", "http://idsvr.com");
        CheckOrigin("http://idsvr.com/test", "http://idsvr.com");
        CheckOrigin("http://idsvr.com/test/resource", "http://idsvr.com");
        CheckOrigin("http://idsvr.com:8080", "http://idsvr.com:8080");
        CheckOrigin("http://idsvr.com:8080/", "http://idsvr.com:8080");
        CheckOrigin("http://idsvr.com:8080/test", "http://idsvr.com:8080");
        CheckOrigin("http://idsvr.com:8080/test/resource", "http://idsvr.com:8080");
        CheckOrigin("http://127.0.0.1", "http://127.0.0.1");
        CheckOrigin("http://127.0.0.1/", "http://127.0.0.1");
        CheckOrigin("http://127.0.0.1/test", "http://127.0.0.1");
        CheckOrigin("http://127.0.0.1/test/resource", "http://127.0.0.1");
        CheckOrigin("http://127.0.0.1:8080", "http://127.0.0.1:8080");
        CheckOrigin("http://127.0.0.1:8080/", "http://127.0.0.1:8080");
        CheckOrigin("http://127.0.0.1:8080/test", "http://127.0.0.1:8080");
        CheckOrigin("http://127.0.0.1:8080/test/resource", "http://127.0.0.1:8080");
        CheckOrigin("http://localhost", "http://localhost");
        CheckOrigin("http://localhost/", "http://localhost");
        CheckOrigin("http://localhost/test", "http://localhost");
        CheckOrigin("http://localhost/test/resource", "http://localhost");
        CheckOrigin("http://localhost:8080", "http://localhost:8080");
        CheckOrigin("http://localhost:8080/", "http://localhost:8080");
        CheckOrigin("http://localhost:8080/test", "http://localhost:8080");
        CheckOrigin("http://localhost:8080/test/resource", "http://localhost:8080");
        CheckOrigin("https://idsvr.com", "https://idsvr.com");
        CheckOrigin("https://idsvr.com/", "https://idsvr.com");
        CheckOrigin("https://idsvr.com/test", "https://idsvr.com");
        CheckOrigin("https://idsvr.com/test/resource", "https://idsvr.com");
        CheckOrigin("https://idsvr.com:8080", "https://idsvr.com:8080");
        CheckOrigin("https://idsvr.com:8080/", "https://idsvr.com:8080");
        CheckOrigin("https://idsvr.com:8080/test", "https://idsvr.com:8080");
        CheckOrigin("https://idsvr.com:8080/test/resource", "https://idsvr.com:8080");
        CheckOrigin("https://127.0.0.1", "https://127.0.0.1");
        CheckOrigin("https://127.0.0.1/", "https://127.0.0.1");
        CheckOrigin("https://127.0.0.1/test", "https://127.0.0.1");
        CheckOrigin("https://127.0.0.1/test/resource", "https://127.0.0.1");
        CheckOrigin("https://127.0.0.1:8080", "https://127.0.0.1:8080");
        CheckOrigin("https://127.0.0.1:8080/", "https://127.0.0.1:8080");
        CheckOrigin("https://127.0.0.1:8080/test", "https://127.0.0.1:8080");
        CheckOrigin("https://127.0.0.1:8080/test/resource", "https://127.0.0.1:8080");
        CheckOrigin("https://localhost", "https://localhost");
        CheckOrigin("https://localhost/", "https://localhost");
        CheckOrigin("https://localhost/test", "https://localhost");
        CheckOrigin("https://localhost/test/resource", "https://localhost");
        CheckOrigin("https://localhost:8080", "https://localhost:8080");
        CheckOrigin("https://localhost:8080/", "https://localhost:8080");
        CheckOrigin("https://localhost:8080/test", "https://localhost:8080");
        CheckOrigin("https://localhost:8080/test/resource", "https://localhost:8080");
        CheckOrigin("test://idsvr.com/test/resource", "test://idsvr.com");
        CheckOrigin("test://idsvr.com:8080/test/resource", "test://idsvr.com:8080");
        CheckOrigin("test://127.0.0.1:8080/test/resource", "test://127.0.0.1:8080");
        CheckOrigin("test://localhost/test/resource", "test://localhost");
        CheckOrigin("test://localhost:8080/test/resource", "test://localhost:8080");
    }

    [Fact]
    [Trait("Category", Category)]
    public void ToSpaceSeparatedString_should_return_correct_value()
    {
        var value = new[] { "foo", "bar", "baz", "baz", "foo", "bar" }.ToSpaceSeparatedString();
        value.ShouldBe("foo bar baz baz foo bar");
    }

    [Fact]
    [Trait("Category", Category)]
    public void FromSpaceSeparatedString_should_return_correct_values()
    {
        var values = "foo bar   baz baz     foo bar".FromSpaceSeparatedString().ToArray();
        values.Length.ShouldBe(6);
        values[0].ShouldBe("foo");
        values[1].ShouldBe("bar");
        values[2].ShouldBe("baz");
        values[3].ShouldBe("baz");
        values[4].ShouldBe("foo");
        values[5].ShouldBe("bar");
    }

    [Fact]
    [Trait("Category", Category)]
    public void FromSpaceSeparatedString_should_only_process_spaces()
    {
        var values = "foo bar\tbaz baz\rfoo bar\r\nbar".FromSpaceSeparatedString().ToArray();
        values.Length.ShouldBe(4);
        values[0].ShouldBe("foo");
        values[1].ShouldBe("bar\tbaz");
        values[2].ShouldBe("baz\rfoo");
        values[3].ShouldBe("bar\r\nbar");
    }


    // scope parsing

    [Fact]
    [Trait("Category", Category)]
    public void Parse_Scopes_with_Empty_Scope_List()
    {
        var scopes = string.Empty.ParseScopesString();

        scopes.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public void Parse_Scopes_with_Sorting()
    {
        var scopes = "scope3 scope2 scope1".ParseScopesString();

        scopes.Count.ShouldBe(3);

        scopes[0].ShouldBe("scope1");
        scopes[1].ShouldBe("scope2");
        scopes[2].ShouldBe("scope3");
    }

    [Fact]
    [Trait("Category", Category)]
    public void Parse_Scopes_with_Extra_Spaces()
    {
        var scopes = "   scope3     scope2     scope1   ".ParseScopesString();

        scopes.Count.ShouldBe(3);

        scopes[0].ShouldBe("scope1");
        scopes[1].ShouldBe("scope2");
        scopes[2].ShouldBe("scope3");
    }

    [Fact]
    [Trait("Category", Category)]
    public void Parse_Scopes_with_Duplicate_Scope()
    {
        var scopes = "scope2 scope1 scope2".ParseScopesString();

        scopes.Count.ShouldBe(2);

        scopes[0].ShouldBe("scope1");
        scopes[1].ShouldBe("scope2");
    }

    [Fact]
    [Trait("Category", Category)]
    public void IsUri_should_allow_uris()
    {
        "https://path".IsUri().ShouldBeTrue();
        "https://path?foo=[x]".IsUri().ShouldBeTrue();
        "file://path".IsUri().ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void IsUri_should_block_paths()
    {
        // especially on linux
        // https://github.com/DuendeSoftware/Support/issues/148
        " /path".IsUri().ShouldBeFalse();
        "/path".IsUri().ShouldBeFalse();
        " //".IsUri().ShouldBeFalse();
        "//".IsUri().ShouldBeFalse();
        "://".IsUri().ShouldBeFalse();
        " ://".IsUri().ShouldBeFalse();
        " file://path".IsUri().ShouldBeFalse();
    }
}