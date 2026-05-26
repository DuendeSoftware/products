// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Represents the result of attempting to create a validated password.
/// </summary>
public abstract record PasswordCreationResult
{
    /// <summary>
    /// The password was successfully created.
    /// </summary>
    /// <param name="Password">The validated plain text password.</param>
    public sealed record Success(PlainTextPassword Password) : PasswordCreationResult;

    /// <summary>
    /// The password could not be created because it failed validation.
    /// </summary>
    /// <param name="Errors">The list of validation errors explaining why the password was rejected.</param>
    public sealed record Failed(IReadOnlyList<string> Errors) : PasswordCreationResult;
}
