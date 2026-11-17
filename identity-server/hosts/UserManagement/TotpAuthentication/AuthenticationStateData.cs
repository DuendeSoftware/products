// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace TotpAuthentication;

public sealed record AuthenticationStateData(string UserId, string Email, bool RememberMe, string? ReturnUrl);
