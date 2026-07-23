// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
///     Serialization DTO for a single attribute value stored in a DSO's
///     <c>ExtendedAttributeValues</c> list. Shared across all configuration store DSOs
///     that support schema-validated extended properties.
/// </summary>
internal sealed record AttributeValueEntryDso(
    string Code,
    string DataType,
    string SerializedValue);
