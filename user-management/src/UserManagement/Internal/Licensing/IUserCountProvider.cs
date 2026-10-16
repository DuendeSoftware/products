// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Internal.Licensing;

/// <summary>
/// Counts the stored user records that fall within the licensed user count boundary.
/// </summary>
/// <remarks>
/// Implementations must either return the complete total or throw. A partial total must never be
/// returned, because it would under-report usage against the licence.
/// </remarks>
internal interface IUserCountProvider
{
    Task<long> GetUserCountAsync(Ct ct);
}
