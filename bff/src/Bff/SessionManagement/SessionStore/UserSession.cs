// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// A user session
/// </summary>
public class UserSession : UserSessionUpdate
{
    /// <summary>
    /// The key
    /// </summary>
    public string Key { get; set; } = default!;

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
        other.Key = Key;
        base.CopyTo(other);
    }
}