// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

partial class SamlXmlReader
{
    /// <inheritdoc/>
    public Task<Response> ReadResponseAsync(
        XmlTraverser source,
        Ct ct) =>
        ReadResponseInternalAsync(source, errorInspector: null, ct);

    /// <inheritdoc/>
    public Task<Response> ReadResponseAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Response>> errorInspector,
        Ct ct) =>
        ReadResponseInternalAsync(source, errorInspector, ct);

    private async Task<Response> ReadResponseInternalAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<Response>>? errorInspector,
        Ct ct)
    {
        Response response = default!;

        if (source.EnsureName(SamlConstants.Elements.Response, SamlConstants.Namespaces.Protocol))
        {
            response = await ReadResponseCoreAsync(source, ct);
        }

        source.MoveNext(true);

        CallErrorInspector(errorInspector, response, source);

        source.ThrowOnErrors();

        return response;
    }

    /// <summary>
    /// Read a Saml Response
    /// </summary>
    /// <param name="source">Source Data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>SamlResponse</returns>
    protected virtual async Task<Response> ReadResponseCoreAsync(XmlTraverser source, Ct ct)
    {
        var samlResponse = Create<Response>();

        ReadAttributes(source, samlResponse);
        await ReadElementsAsync(source.GetChildren(), samlResponse, ct);

        return samlResponse;
    }

    /// <summary>
    /// Read elements of SamlResponse
    /// </summary>
    /// <param name="source">XmlTraverser</param>
    /// <param name="response">Response to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, Response response, Ct ct)
    {
        await ReadElementsAsync(source, (StatusResponseType)response, ct);

        while (source.HasName(SamlConstants.Elements.Assertion, SamlConstants.Namespaces.Assertion))
        {
            response.Assertions.Add(await ReadAssertionCoreAsync(source, ct));
            source.MoveNext(true);
        }
    }
}
