// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// A user session
/// </summary>
public class UserSession : UserSessionUpdate
{
    /// <summary>
    /// The key
    /// </summary>
    public UserKey? Key { get; set; }

    public PartitionKey? PartitionKey { get; set; }

    internal UserSessionKey GetUserSessionKey()
    {
        if (!PartitionKey.HasValue)
        {
            throw new ArgumentNullException(nameof(PartitionKey));
        }

        if (!Key.HasValue)
        {
            throw new ArgumentNullException(nameof(Key));
        }

        return new UserSessionKey(PartitionKey.Value, Key.Value);
    }

    /// <summary>
    /// Clones the instance
    /// </summary>
    /// <returns></returns>
    public UserSession Clone()
    {
        var other = new UserSession();
        CopyTo(other);
        return other;
    }

    /// <summary>
    /// Copies this instance into another
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public void CopyTo(UserSession other)
    {
        ArgumentNullException.ThrowIfNull(other);
        other.Key = Key;
        other.PartitionKey = PartitionKey;
        base.CopyTo(other);
    }
}
