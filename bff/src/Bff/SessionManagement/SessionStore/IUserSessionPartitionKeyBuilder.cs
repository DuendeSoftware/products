// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// Allows you to configure a partition key for user sessions.
/// Usually, they are partitioned by application name an optionally by frontend, but this can be customized.
/// </summary>
public interface IUserSessionPartitionKeyBuilder
{
    /// <summary>
    /// Returns the partition key. The v3 implementation allowed to return null
    /// but this can cause issues, because a null value is ignored from indexes.
    /// For backwards compat, this interface allows returning null, but it is recommended
    /// to return default string instead.
    /// </summary>
    /// <returns></returns>
    string? BuildPartitionKey();
}
