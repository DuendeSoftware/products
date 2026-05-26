// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Internal.Storage;

/// <summary>
/// Provides access to an <see cref="IStore"/> for user management data.
/// </summary>
internal interface IStoreFactory
{
    /// <summary>
    /// Gets a store instance.
    /// </summary>
    IStore GetStore();
}
