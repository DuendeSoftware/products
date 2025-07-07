// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Security.Cryptography;

namespace Duende.IdentityServer.Services;

/// <summary>
/// User code generator using 9 digit number
/// </summary>
/// <seealso cref="IUserCodeGenerator" />
public class NumericUserCodeGenerator : IUserCodeGenerator
{
    /// <summary>
    /// Gets the type of the user code.
    /// </summary>
    /// <value>
    /// The type of the user code.
    /// </value>
    public string UserCodeType => IdentityServerConstants.UserCodeTypes.Numeric;

    /// <summary>
    /// Gets the retry limit.
    /// </summary>
    /// <value>
    /// The retry limit for getting a unique value.
    /// </value>
    public int RetryLimit => 5;

    /// <summary>
    /// Generates the user code.
    /// </summary>
    /// <returns></returns>
    public Task<string> GenerateAsync()
    {
        var next = RandomNumberGenerator.GetInt32(100000000, 1000000000);
        return Task.FromResult(next.ToString(CultureInfo.InvariantCulture));
    }
}
