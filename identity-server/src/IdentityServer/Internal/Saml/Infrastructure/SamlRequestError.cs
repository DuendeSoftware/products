// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal enum SamlRequestErrorType
{
    Validation,
    Protocol
}

internal class SamlRequestError<TRequest>
{
    internal SamlRequestErrorType Type { get; init; }
    internal string? ValidationMessage { get; init; }
    internal SamlProtocolError<TRequest>? ProtocolError { get; init; }
}

internal record SamlProtocolError<TRequest>(
    SamlServiceProvider ServiceProvider,
    TRequest Request,
    SamlError Error);
