// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.Storage.Internal.Outbox;

internal sealed class OutboxProcessorOptionsValidator : IValidateOptions<OutboxProcessorOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxProcessorOptions options) =>
        options.MaxRetries < 0
            ? ValidateOptionsResult.Fail(
                "OutboxProcessorOptions.MaxRetries must be 0 or greater. " +
                "Set it to 0 to drop a failing event after its first attempt.")
            : ValidateOptionsResult.Success;
}
