// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Diagnostics;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models access to an API scope
/// </summary>
[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class ApiScope : Resource
{
    private string DebuggerDisplay => Name ?? $"{{{typeof(ApiScope)}}}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    public ApiScope()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    public ApiScope(string name)
        : this(name, name, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    public ApiScope(string name, string displayName)
        : this(name, displayName, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="userClaims">List of associated user claims that should be included when this resource is requested.</param>
    public ApiScope(string name, IEnumerable<string> userClaims)
        : this(name, name, userClaims)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiScope"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="userClaims">List of associated user claims that should be included when this resource is requested.</param>
    /// <exception cref="System.ArgumentNullException">name</exception>
    public ApiScope(string name, string displayName, IEnumerable<string>? userClaims)
    {
        if (name.IsMissing())
        {
            throw new ArgumentNullException(nameof(name));
        }

        Name = name;
        DisplayName = displayName;

        if (!userClaims.IsNullOrEmpty())
        {
            foreach (var type in userClaims!)
            {
                UserClaims.Add(type);
            }
        }
    }

    /// <summary>
    /// Specifies whether the user can de-select the scope on the consent screen. Defaults to false.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Specifies whether the consent screen will emphasize this scope. Use this setting for sensitive or important scopes. Defaults to false.
    /// </summary>
    public bool Emphasize { get; set; } = false;
}
