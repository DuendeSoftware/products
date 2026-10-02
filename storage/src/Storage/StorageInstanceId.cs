// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Identifies a configured storage instance (an engine plus its connection target) used to route
/// keyed DI resolution of storage providers. Identifiers are compared using ordinal, case-sensitive
/// equality: casing is significant and never normalized.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[StringValue]
public partial record StorageInstanceId
{
    /// <summary>
    /// The identifier used for the default (unkeyed) storage instance registration.
    /// </summary>
    internal const string DefaultIdentifier = "default";

    /// <summary>
    /// The default storage instance, corresponding to the unkeyed storage registration.
    /// </summary>
    public static readonly StorageInstanceId Default = Load(DefaultIdentifier);

    /// <summary>
    /// Gets a value indicating whether this instance is the <see cref="Default"/> storage instance.
    /// </summary>
    public bool IsDefault => this == Default;
}
