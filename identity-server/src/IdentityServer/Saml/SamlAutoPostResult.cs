// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Endpoints.Results;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// An <see cref="IResult"/> that writes a SAML response to the browser via the
/// appropriate front-channel binding (e.g., HTTP-POST auto-submit form).
/// Internally delegates to the IdentityServer <see cref="Saml2FrontChannelResult"/>
/// pipeline, which resolves the correct binding and response writer from DI.
/// </summary>
public sealed class SamlAutoPostResult : IResult
{
    internal Saml2FrontChannelResult FrontChannelResult { get; }

    public SamlAutoPostResult(Saml2FrontChannelResult frontChannelResult) =>
        FrontChannelResult = frontChannelResult ?? throw new ArgumentNullException(nameof(frontChannelResult));

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext) => FrontChannelResult.ExecuteAsync(httpContext);
}
