// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.CliPlugin.Commands;

public sealed class MigrateCommandTests
{
    [Fact]
    public void Provider_is_required()
    {
        var command = MigrateCommand.Create();
        var parseResult = command.Parse(["--connection-string", "foo"]);

        parseResult.Errors.ShouldContain(e => e.Message.Contains("--provider"));
    }
}
