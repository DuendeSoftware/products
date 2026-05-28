// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

public partial class SamlXmlReader
{
    /// <inheritdoc/>
    public async Task<LogoutRequest> ReadLogoutRequestAsync(
        XmlTraverser source,
        Action<ReadErrorInspectorContext<LogoutRequest>>? errorInspector,
        Ct ct)
    {
        LogoutRequest logoutRequest = default!;

        if (source.EnsureName(SamlConstants.Elements.LogoutRequest, SamlConstants.Namespaces.Protocol))
        {
            logoutRequest = await ReadLogoutRequestCoreAsync(source, ct);
            source.MoveNext(true);
        }

        CallErrorInspector(errorInspector, logoutRequest, source);

        source.ThrowOnErrors();

        return logoutRequest;
    }

    /// <summary>
    /// Read a <see cref="LogoutRequest"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The <see cref="LogoutRequest"/> read</returns>
    protected async Task<LogoutRequest> ReadLogoutRequestCoreAsync(XmlTraverser source, Ct ct)
    {
        var logoutRequest = Create<LogoutRequest>();

        ReadAttributes(source, logoutRequest);
        await ReadElementsAsync(source.GetChildren(), logoutRequest, ct);

        return logoutRequest;
    }

    /// <summary>
    /// Reads attributes of a <see cref="LogoutRequest"/>
    /// </summary>
    /// <param name="source">Xml Traverser to read from</param>
    /// <param name="logoutRequest">The <see cref="LogoutRequest"/> to populate</param>
    protected virtual void ReadAttributes(XmlTraverser source, LogoutRequest logoutRequest)
    {
        ReadAttributes(source, (RequestAbstractType)logoutRequest);

        logoutRequest.Reason = source.GetAbsoluteUriAttribute(SamlConstants.Attributes.Reason);
        logoutRequest.NotOnOrAfter = source.GetDateTimeAttribute(SamlConstants.Attributes.NotOnOrAfter);
    }

    /// <summary>
    /// Reads the child elements of a <see cref="LogoutRequest"/>
    /// </summary>
    /// <param name="source">Xml traverser to read from</param>
    /// <param name="logoutRequest"><see cref="LogoutRequest"/> to populate</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual async Task ReadElementsAsync(XmlTraverser source, LogoutRequest logoutRequest, Ct ct)
    {
        await ReadElementsAsync(source, (RequestAbstractType)logoutRequest, ct);

        // NameID is mandatory per SAML 2.0 Core §3.7.1
        if (source.EnsureName(SamlConstants.Elements.NameID, SamlConstants.Namespaces.Assertion))
        {
            logoutRequest.NameId = ReadNameId(source);
            source.MoveNext(true);
        }

        if (source.HasName(SamlConstants.Elements.SessionIndex, SamlConstants.Namespaces.Protocol))
        {
            logoutRequest.SessionIndex = source.GetTextContents();
            source.MoveNext(true);
        }
    }
}
