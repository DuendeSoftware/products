// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Services;

internal interface IMtlsEndpointGenerator
{
    string GetMtlsEndpointPath(string endpoint);
}
