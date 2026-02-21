// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Validation;

public interface IIssuerPathValidator
{
    /// <summary>
    /// Validates that the path is valid for issuer URIs used.
    /// </summary>
    /// <param name="path">A path component of a URI to validate against the issuer for the current request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the path component is valid in for the issuer in the context of the current request.</returns>
    Task<bool> ValidateAsync(string path, CT ct);
}
