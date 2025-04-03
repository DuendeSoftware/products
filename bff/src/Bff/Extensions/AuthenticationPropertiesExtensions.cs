// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authentication;

// ReSharper disable once CheckNamespace
namespace Duende.Bff;

/// <summary>
///  Extension methods for AuthenticationProperties
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public static class AuthenticationPropertiesExtensions
{
    /// <summary>
    /// Determines if this AuthenticationProperties represents a BFF silent login.
    /// </summary>
    public static bool IsSilentLogin(this AuthenticationProperties props)
    {
        return props.TryGetPrompt(out var prompt) && prompt == "none";
    }

    public static bool TryGetPrompt(this AuthenticationProperties props, [NotNullWhen(true)] out string? prompt)
    {
        return props.Items.TryGetValue(Constants.BffFlags.Prompt, out prompt);
    }
}
