// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal sealed record PasswordAuthResult(bool Authenticated, bool NeedsRehash);
