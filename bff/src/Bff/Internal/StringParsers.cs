// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Internal;

internal static class StringParsers<TSelf> where TSelf : struct, IStronglyTypedValue<TSelf>
{
    internal static TSelf Parse(string value)
    {
        if (TSelf.TryParse(value, out var parseResult, out var errors))
        {
            return parseResult.Value;
        }

        throw new InvalidOperationException(
            $"Received an invalid {typeof(TSelf).Name}: '{value}'. Errors: {string.Join("", errors.Select(x => $"{Environment.NewLine}\t - {x}"))}");
    }

    internal static TSelf? ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TSelf.TryParse(value, out var parseResult, out var errors))
        {
            return parseResult;
        }

        throw new InvalidOperationException(
            $"Received an invalid {typeof(TSelf).Name}. Errors: {string.Join("", errors.Select(x => $"{Environment.NewLine}\t - {x}"))}");
    }
}
