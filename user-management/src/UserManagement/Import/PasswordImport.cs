// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;

namespace Duende.UserManagement.Import;

/// <summary>
/// Represents a pre-hashed password to import from a source system. The platform stores it as-is
/// and verifies it using the registered <see cref="IPasswordHashAlgorithm"/> for the stored algorithm ID.
/// On first successful authentication, the password is transparently re-hashed using the
/// current preferred algorithm.
/// </summary>
public sealed record PasswordImport(HashedPasswordData Data);
