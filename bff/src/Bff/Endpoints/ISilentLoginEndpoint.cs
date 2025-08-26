// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.Endpoints;

/// <summary>
/// Service for handling silent login requests
/// </summary>
[Obsolete(
    "The silent login endpoint will be removed in a future version. Silent login is now handled by passing the prompt=none parameter to the login endpoint.")]
public interface ISilentLoginEndpoint : IBffEndpoint;
