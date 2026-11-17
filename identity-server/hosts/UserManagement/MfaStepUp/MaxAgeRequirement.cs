// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace MfaStepUp;

internal sealed record MaxAgeRequirement(TimeSpan MaxAge) : IAuthorizationRequirement;
