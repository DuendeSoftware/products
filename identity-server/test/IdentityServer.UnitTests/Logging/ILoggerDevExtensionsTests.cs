// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;

namespace IdentityServer.UnitTests.Logging;

public sealed class ILoggerDevExtensionsTests
{
    [Fact]
    public void SanitizeLogParameter_removes_newlines_and_control_characters()
    {
        var input = "safe\r\nnew\nline\rold\u0000\u0008\u0009\u0085\u2028\u2029end";

        input.SanitizeLogParameter().ShouldBe("safenewlineoldend");
    }

    [Fact]
    public void SanitizeLogParameter_preserves_safe_string()
    {
        const string input = "safe text !@#$%^&*() café 😀";

        input.SanitizeLogParameter().ShouldBe(input);
    }

    [Fact]
    public void SanitizeLogParameter_preserves_empty_string() =>
        string.Empty.SanitizeLogParameter().ShouldBe(string.Empty);

    [Fact]
    public void SanitizeLogParameter_preserves_non_string_value()
    {
        var input = new object();

        input.SanitizeLogParameter().ShouldBeSameAs(input);
    }
}
