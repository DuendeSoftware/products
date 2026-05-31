// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;
using InternalUserAuthenticators = Duende.UserManagement.Authentication.Internal.UserAuthenticators;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class OtpAuthenticator(
    OtpVerifier otpVerifier,
    UserAuthenticatorsRepository authenticatorsRepository,
    ILogger<OtpAuthenticator> logger,
    UserManagementLicenseValidator licenseValidator) : IOtpAuthenticator
{
    public async Task<OtpAuthenticationResult> TryAuthenticateAsync(PlainTextOtp otp, OtpToken token, Ct ct)
    {
        licenseValidator.ValidateOtp();
        logger.OtpVerificationStarted(LogLevel.Debug);

        var otpAddress = await otpVerifier.TryVerifyAsync(otp, token, ct);

        if (otpAddress is null)
        {
            return OtpAuthenticationResult.Failure.Instance;
        }

        logger.OtpVerificationSucceeded(LogLevel.Information, otpAddress);

        UserSubjectId subjectId;
        if (await authenticatorsRepository.TryReadAsync(otpAddress, ct) is { } authenticatorsRecord)
        {
            subjectId = authenticatorsRecord.UserAuthenticators.SubjectId;
        }
        else
        {
            var authenticators = new InternalUserAuthenticators(UserSubjectId.New(), [otpAddress], []);
            if (await authenticatorsRepository.CreateAsync(authenticators, ct) is not CreateResult.Success)
            {
                return OtpAuthenticationResult.Failure.Instance;
            }

            licenseValidator.ValidateUserCount();
            subjectId = authenticators.SubjectId;
        }

        return new OtpAuthenticationResult.Success(otpAddress, subjectId);
    }
}
