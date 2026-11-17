// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// A test user created with <see cref="ITestUsers.Create" /> (design doc D14): complete and valid as created, with a
/// generated subject ID and default password the test reads instead of repeating them as literals.
/// </summary>
public sealed class TestUser
{
    internal TestUser(string username, string subjectId, string password, string issuer)
    {
        Issuer = issuer;
        Username = username;
        SubjectId = subjectId;
        Password = password;
    }

    /// <summary>Unique to the issuer the user was created for, for example <c>alice-sut-3f2a91c0</c>.</summary>
    public string Username { get; }

    /// <summary>The issuer the user was created for.</summary>
    public string Issuer { get; }

    public string SubjectId { get; }

    public string Password { get; }
}
