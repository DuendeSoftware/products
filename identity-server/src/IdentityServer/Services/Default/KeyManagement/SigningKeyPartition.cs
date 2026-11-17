// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Maps the current Space context to the key management partition. The signing key cache and the
/// signing key creation lock must use the same partition.
/// </summary>
/// <remarks>
/// This follows the Spaces convention for "no Space": work that runs without a Space set maps to the Default
/// Space, which is how Spaces-aware storage selects its data. Requests without a Space and requests in the
/// Default Space therefore share stored keys, so they must also share cached keys and the creation lock.
/// Without Spaces installed there is a single partition.
/// <para>
/// A custom <see cref="Stores.ISigningKeyStore"/> that keeps keys per Space must follow the same convention and
/// return the Default Space's keys when no Space is set. Otherwise work without a Space and the Default Space
/// would share one cache entry and creation lock while the store gives them different keys. The public
/// contract for store authors is on <see cref="Stores.ISigningKeyStore"/>.
/// </para>
/// </remarks>
internal static class SigningKeyPartition
{
    internal const string Default = "__default__";

    internal static string For(ISpaceContextAccessor? spaceContextAccessor) =>
        spaceContextAccessor is null
            ? Default
            : spaceContextAccessor.GetSpaceIdOrDefault().Value.ToString();
}
