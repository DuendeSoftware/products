// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
/// Allows validating if the return URL for login and logout is valid.
/// </summary>
public interface IReturnUrlValidator
{
    /// <summary>
    /// Returns true is the returnUrl is valid and safe to redirect to.
    /// </summary>
    /// <param name="returnUrl"></param>
    /// <returns></returns>
    public Task<bool> IsValidAsync(string returnUrl);
}
