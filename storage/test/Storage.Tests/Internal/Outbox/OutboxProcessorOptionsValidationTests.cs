// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Outbox;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Storage.Internal;

/// <summary>
/// Verifies that <see cref="OutboxProcessorOptions.MaxRetries"/> is validated at startup: a
/// negative value fails both when a host runs <c>ValidateOnStart</c> and, without a host, the
/// first time <see cref="OutboxProcessor"/> is resolved.
/// </summary>
public sealed class OutboxProcessorOptionsValidationTests
{
    [Fact]
    public void negative_max_retries_fails_options_validation_at_startup()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        _ = services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = -1);

        using var provider = services.BuildServiceProvider();

        var startupValidator = provider.GetRequiredService<IStartupValidator>();
        var validationException = Should.Throw<OptionsValidationException>(() => startupValidator.Validate());
        validationException.Message.ShouldContain(nameof(OutboxProcessorOptions.MaxRetries));

        var resolveException = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<OutboxProcessor>());
        resolveException.Message.ShouldContain(nameof(OutboxProcessorOptions.MaxRetries));
    }

    [Fact]
    public void zero_max_retries_validates_successfully()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        _ = services.Configure<OutboxProcessorOptions>(o => o.MaxRetries = 0);

        using var provider = services.BuildServiceProvider();

        var startupValidator = provider.GetRequiredService<IStartupValidator>();
        Should.NotThrow(() => startupValidator.Validate());
        _ = Should.NotThrow(() => provider.GetRequiredService<OutboxProcessor>());
    }
}
