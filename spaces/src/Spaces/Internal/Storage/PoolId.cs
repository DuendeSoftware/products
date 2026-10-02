// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.Spaces.Internal.Storage;

/// <summary>
/// Extension members for <see cref="PoolId"/>.
/// </summary>
internal static class PoolIdExtensions
{
    extension(PoolId _)
    {
        /// <summary>
        /// The well-known pool id used for management storage.
        /// </summary>
        public static PoolId Management => -1;
    }
}
