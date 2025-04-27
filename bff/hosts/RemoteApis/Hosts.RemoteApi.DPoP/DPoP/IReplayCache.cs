// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Api.DPoP;

public interface IReplayCache
{
    /// <summary>
    /// Adds a handle to the cache 
    /// </summary>
    /// <param name="purpose"></param>
    /// <param name="handle"></param>
    /// <param name="expiration"></param>
    Task AddAsync(string purpose, string handle, DateTimeOffset expiration);


    /// <summary>
    /// Checks if a cached handle exists 
    /// </summary>
    /// <param name="purpose"></param>
    /// <param name="handle"></param>
    Task<bool> ExistsAsync(string purpose, string handle);
}
