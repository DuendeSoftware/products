// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin;

/// <summary>
/// Test-only helper for looking up a single <see cref="AttributeValue"/> by <see cref="AttributeCode"/>
/// from a read-only extended-properties collection. Five of the six admin entities expose
/// <c>ExtendedProperties</c> as <see cref="IReadOnlyCollection{T}"/> of <see cref="AttributeValue"/> on
/// their sealed read model (unlike <see cref="AttributeValueCollection"/>, which offers a built-in
/// <c>TryGet</c> method); this extension restores the same lookup ergonomics for tests.
/// </summary>
internal static class AttributeValueCollectionExtensions
{
    public static bool TryGet(
        this IReadOnlyCollection<AttributeValue> values,
        AttributeCode code,
        [MaybeNullWhen(false)] out AttributeValue attribute)
    {
        foreach (var value in values)
        {
            if (value.Code == code)
            {
                attribute = value;
                return true;
            }
        }

        attribute = null;
        return false;
    }
}
