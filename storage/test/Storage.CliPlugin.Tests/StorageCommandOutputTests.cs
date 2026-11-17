// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.CliPlugin.Commands;

public sealed class StorageCommandOutputTests
{
    [Fact]
    public void Sanitizer_redacts_connection_string_fields()
    {
        const string message =
            "Authentication failed with Host=private.example; User ID=command-user; Password=secret; Database=storage";

        var result = StorageCommandOutput.SanitizeMessage(message);

        result.ShouldNotContain("private.example");
        result.ShouldNotContain("command-user");
        result.ShouldNotContain("secret");
        result.ShouldContain("[redacted]");
    }
}
