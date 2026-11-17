// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Marker service signaling that in-process SCIM token validation is available, so
/// <see cref="ScimOAuthOptions.Authority"/> isn't required. Registered by <c>EnableScim</c>; used by
/// <c>ScimOAuthOptionsValidator</c> to skip the <c>Authority</c> requirement.
/// </summary>
internal sealed class ScimLocalIssuerMarker;
