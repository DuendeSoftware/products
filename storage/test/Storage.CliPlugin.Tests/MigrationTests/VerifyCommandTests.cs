// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;

namespace Duende.Storage.CliPlugin.Commands;

[CollectionDefinition("StorageCliEnvironment", DisableParallelization = true)]
public sealed class StorageCliEnvironment;

[Collection("StorageCliEnvironment")]
public sealed class VerifyCommandTests
{
    [Fact]
    public async Task Plugin_exposes_verify_and_normalizes_provider_names()
    {
        var command = new StorageCliPlugin().GetCommand();
        var parseResult = command.Parse(["verify", "--provider", "SQLITE", "--connection-string", "Data Source=:memory:"]);

        parseResult.Errors.ShouldBeEmpty();
        (await parseResult.InvokeAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Verify_rejects_sqlite_schema()
    {
        var parseResult = VerifyCommand.Create().Parse(
            ["--provider", "sqlite", "--connection-string", "Data Source=:memory:", "--schema", "not-supported"]);

        (await parseResult.InvokeAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Verify_rejects_unknown_provider()
    {
        var parseResult = VerifyCommand.Create().Parse(
            ["--provider", "unknown", "--connection-string", "Data Source=:memory:"]);

        (await parseResult.InvokeAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Environment_connection_string_is_used_when_option_is_omitted() =>
        await WithConnectionStringEnvironmentAsync("Data Source=:memory:", async () =>
        {
            var parseResult = VerifyCommand.Create().Parse(["--provider", "sqlite"]);
            (await parseResult.InvokeAsync()).ShouldBe(2);
        });

    [Fact]
    public async Task Explicit_connection_string_takes_precedence_over_the_environment() =>
        await WithConnectionStringEnvironmentAsync("not-a-valid-connection-string", async () =>
        {
            var parseResult = VerifyCommand.Create().Parse(
                ["--provider", "sqlite", "--connection-string", "Data Source=:memory:"]);
            (await parseResult.InvokeAsync()).ShouldBe(2);
        });

    [Fact]
    public async Task Missing_connection_string_is_rejected() =>
        await WithConnectionStringEnvironmentAsync(null, async () =>
        {
            var parseResult = VerifyCommand.Create().Parse(["--provider", "sqlite"]);
            (await parseResult.InvokeAsync()).ShouldBe(1);
        });

    private static async Task WithConnectionStringEnvironmentAsync(string? value, Func<Task> action)
    {
        const string variable = "DUENDE_STORAGE_CONNECTION_STRING";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, value);
            await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Theory]
    [InlineData("postgresql", "public")]
    [InlineData("mssql", "dbo")]
    [InlineData("oracle", null)]
    public async Task Shared_options_preserve_provider_schema_defaults(string provider, string? expectedSchema)
    {
        var command = new Command("verify");
        var options = new StorageCommandOptions(command, "verify");
        var parseResult = command.Parse(["--provider", provider, "--connection-string", "not-used"]);

        var arguments = await options.ResolveAsync(parseResult);

        arguments.HasValue.ShouldBeTrue();
        arguments.Value.SchemaName.ShouldBe(expectedSchema);
    }
}
