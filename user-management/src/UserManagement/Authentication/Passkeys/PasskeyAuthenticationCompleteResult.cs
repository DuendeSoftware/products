// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Result of completing passkey authentication.
/// </summary>
public abstract record PasskeyAuthenticationCompleteResult
{
    private PasskeyAuthenticationCompleteResult() { }

    /// <summary>
    /// Authentication completed successfully.
    /// </summary>
    public sealed record Success : PasskeyAuthenticationCompleteResult
    {
        internal Success(
            UserSubjectId userSubjectId,
            PasskeyCredentialId credentialId,
            uint signCount,
            bool userVerified,
            bool backedUp)
        {
            UserSubjectId = userSubjectId;
            CredentialId = credentialId;
            SignCount = signCount;
            UserVerified = userVerified;
            BackedUp = backedUp;
        }

        /// <summary>The subject ID of the authenticated user.</summary>
        public UserSubjectId UserSubjectId { get; }

        /// <summary>The authenticator credential ID that was used.</summary>
        public PasskeyCredentialId CredentialId { get; }

        /// <summary>The updated signature counter.</summary>
        public uint SignCount { get; }

        /// <summary>Whether user verification was performed.</summary>
        public bool UserVerified { get; }

        /// <summary>Whether the credential is currently backed up.</summary>
        public bool BackedUp { get; }
    }

    /// <summary>
    /// Authentication failed with an error.
    /// </summary>
    public sealed record Failure : PasskeyAuthenticationCompleteResult
    {
        internal Failure(AuthenticationCompleteError error, string errorDescription)
        {
            Error = error;
            ErrorDescription = errorDescription;
        }

        /// <summary>The error code.</summary>
        public AuthenticationCompleteError Error { get; }

        /// <summary>Human-readable error description.</summary>
        public string ErrorDescription { get; }
    }
}
