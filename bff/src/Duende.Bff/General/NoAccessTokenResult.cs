// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Represents that no access token exists. This type should be used when the
/// access token is optional and the absence of a token is not an error.
/// </summary>
public class NoAccessTokenResult : AccessTokenResult {}
