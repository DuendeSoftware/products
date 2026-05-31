// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SmtpOtpDispatcherOptionsValidator() : DataAnnotationValidateOptions<SmtpOtpDispatcherOptions>(null);
