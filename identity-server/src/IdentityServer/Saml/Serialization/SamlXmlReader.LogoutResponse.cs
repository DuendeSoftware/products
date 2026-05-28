// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <inheritdoc/>
    public async Task<LogoutResponse> ReadLogoutResponseAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<LogoutResponse>>? errorInspector,
        Ct ct)
    {
        LogoutResponse logoutResponse = default!;

        if (source.EnsureName(SamlConstants.Elements.LogoutResponse, SamlConstants.Namespaces.Protocol))
        {
            logoutResponse = await ReadLogoutResponseCoreAsync(source, ct);
            source.MoveNext(true);
        }

        CallErrorInspector(errorInspector, logoutResponse, source);

        source.ThrowOnErrors();

        return logoutResponse;
    }

    /// <summary>
    /// Read a <see cref="LogoutResponse"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The <see cref="LogoutResponse"/> read</returns>
    protected async Task<LogoutResponse> ReadLogoutResponseCoreAsync(XmlTraverser source, Ct ct)
    {
        var logoutResponse = Create<LogoutResponse>();

        ReadAttributes(source, logoutResponse);
        await ReadElementsAsync(source.GetChildren(), logoutResponse, ct);

        return logoutResponse;
    }
}
