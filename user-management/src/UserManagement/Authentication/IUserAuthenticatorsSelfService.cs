// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Self-service interface for users to manage their own authenticators, including passwords, OTP addresses,
/// external authenticator addresses, TOTP devices, passkeys, and recovery codes.
/// </summary>
public interface IUserAuthenticatorsSelfService
{
    /// <summary>
    /// Creates a validated <see cref="ValidatedPlainTextPassword"/> from the given string, applying all configured password validators.
    /// This password can then be used to set the password using <see cref="TrySetPasswordAsync"/>.
    /// Throws if validation fails.
    /// </summary>
    /// <Remarks>
    ///  This method does NOT set the password on the user. It only validates the password string and returns the result.
    ///  Call <see cref="TrySetPasswordAsync"/> to actually set the password on the user's authenticator record.
    /// </Remarks>
    /// <param name="userId">The subject ID of the user the password is being created for.</param>
    /// <param name="passwordString">The plain text password string to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ValidatedPlainTextPassword> ValidatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct);

    /// <summary>
    /// Attempts to create a validated <see cref="ValidatedPlainTextPassword"/> from the given string.
    /// This password can then be used to set the password using <see cref="TrySetPasswordAsync"/>.
    ///
    /// Returns a <see cref="PasswordCreationResult"/> indicating success or validation failure.
    /// </summary>
    /// <Remarks>
    ///  This method does NOT set the password on the user. It only validates the password string and returns the result.
    ///  Call <see cref="TrySetPasswordAsync"/> to actually set the password on the user's authenticator record.
    /// </Remarks>
    /// <param name="userId">The subject ID of the user the password is being created for.</param>
    /// <param name="passwordString">The plain text password string to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PasswordCreationResult> TryValidatePasswordAsync(UserSubjectId userId, string passwordString, Ct ct);

    /// <summary>
    /// Retrieves the authenticator record for the specified user by subject ID.
    /// Returns <c>null</c> if no record exists.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Adds an OTP address to the specified user's authenticator record after verifying the OTP.
    /// Returns <c>false</c> if the user record does not exist or OTP verification fails.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="otp">The OTP to verify ownership of the address.</param>
    /// <param name="token">The token returned by <see cref="Otp.IOtpSender.TrySendOtpAsync"/> identifying the OTP challenge (which contains the address).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddOtpAddressAsync(UserSubjectId subjectId, PlainTextOtp otp, OtpToken token, Ct ct);

    /// <summary>
    /// Removes an OTP address from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="address">The OTP address to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRemoveOtpAddressAsync(UserSubjectId subjectId, OtpAddress address, Ct ct);

    /// <summary>
    /// Adds an external authenticator address to the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="address">The external authenticator address to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddExternalAuthenticatorAddressAsync(UserSubjectId subjectId, ExternalAuthenticatorAddress address, Ct ct);

    /// <summary>
    /// Removes an external authenticator address from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="address">The external authenticator address to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRemoveExternalAuthenticatorAddressAsync(UserSubjectId subjectId, ExternalAuthenticatorAddress address, Ct ct);

    /// <summary>
    /// Adds a TOTP device to the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist or the TOTP code is invalid.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="deviceName">The name of the TOTP device.</param>
    /// <param name="key">The TOTP secret key bytes.</param>
    /// <param name="totp">The current TOTP code to verify the key is correct.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddTotpDeviceAsync(UserSubjectId subjectId, TotpDeviceName deviceName, PlainBytesTotpKey key, PlainTextTotp totp, Ct ct);

    /// <summary>
    /// Removes a TOTP device from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="deviceName">The name of the TOTP device to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRemoveTotpDeviceAsync(UserSubjectId subjectId, TotpDeviceName deviceName, Ct ct);

    /// <summary>
    /// Adds a passkey credential to the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="credential">The passkey credential data to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddPasskeyAsync(UserSubjectId subjectId, PasskeyCredentialData credential, Ct ct);

    /// <summary>
    /// Removes a passkey credential from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="credentialId">The ID of the passkey credential to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRemovePasskeyAsync(UserSubjectId subjectId, PasskeyCredentialId credentialId, Ct ct);

    /// <summary>
    /// Generates a new set of recovery codes for the specified user, replacing any existing codes.
    /// Returns <c>null</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyCollection<PlainTextRecoveryCode>?> TryCreateRecoveryCodesAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Sets a new password for the specified user without requiring the current password.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <Remarks>
    /// The user must already have an authenticator before you can create a password on it.
    /// If you are not using any other form of authentication, then you must first call <see cref="IUserAuthenticatorsAdmin.TryAddAsync"/> to
    /// create an empty Authenticator.
    ///
    /// IE: await authenticatorsAdmin.TryAddAsync(profile.SubjectId, [], [], cancellationToken);
    /// </Remarks>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="password">The new validated password.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TrySetPasswordAsync(UserSubjectId subjectId, ValidatedPlainTextPassword password, Ct ct);

    /// <summary>
    /// Changes the user's password by verifying the current password before applying the new one.
    /// Returns <c>false</c> if the user record does not exist or the current password is incorrect.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="oldPassword">The current password to verify.</param>
    /// <param name="newPassword">The new validated password.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryChangePasswordAsync(UserSubjectId subjectId, NonValidatedPassword oldPassword, ValidatedPlainTextPassword newPassword, Ct ct);

    /// <summary>
    /// Resets the user's password without verifying the current password.
    /// Intended for administrative or token-based reset flows.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="password">The new validated password.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<bool> TryResetPasswordAsync(UserSubjectId subjectId, ValidatedPlainTextPassword password, Ct ct);
}
