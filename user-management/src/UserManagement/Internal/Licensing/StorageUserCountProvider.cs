// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Storage;
namespace Duende.UserManagement.Internal.Licensing;

/// <summary>
/// Counts user records in the storage partition resolved for the current context. Used when no
/// host integration supplies a broader counting boundary.
/// </summary>
internal sealed class StorageUserCountProvider(UserRepository userRepository) : IUserCountProvider
{
    public Task<long> GetUserCountAsync(Ct ct) => userRepository.GetCountAsync(ct);
}
