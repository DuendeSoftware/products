// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Cryptography.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace Duende.IdentityServer.Saml.Serialization;

/// <summary>
/// Reader for data from an Xml Document.
/// </summary>
public partial class SamlXmlReader : ISamlXmlReader
{
    /// <inheritdoc/>
    public virtual IEnumerable<string>? AllowedAlgorithms { get; set; }

    /// <inheritdoc/>
    public virtual IEnumerable<SigningKey>? TrustedSigningKeys { get; set; }

    /// <inheritdoc/>
    public virtual Func<string, Ct, Task<Saml2Entity?>>? EntityResolver { get; set; }

    /// <summary>
    /// Helper method that calls ThrowOnErrors. To supress errors and prevent
    /// throwing, this is the last chance method to override.
    /// </summary>
    protected virtual void ThrowOnErrors(XmlTraverser source)
        => source.ThrowOnErrors();

    /// <summary>
    /// Default factory for read types is just to new it up. Override this method
    /// to create a derived/specialized type instead.
    /// </summary>
    /// <typeparam name="T">Type to create</typeparam>
    /// <returns>New instance of <typeparamref name="T"/></returns>
    protected virtual T Create<T>() where T : new() => new();

    /// <summary>
    /// Helper method to get the signing keys and allowed signature algorithms for
    /// an issuer by invoking <see cref="EntityResolver"/>.
    /// </summary>
    /// <param name="source">Xml Traverser source - used to report errors.</param>
    /// <param name="issuer">The issuer to find parameters for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Trusted signing keys and allowed algorithms. When <see cref="EntityResolver"/>
    /// is set, its result is used unconditionally. Falls back to
    /// <see cref="TrustedSigningKeys"/> and <see cref="AllowedAlgorithms"/>
    /// only when <see cref="EntityResolver"/> is null.
    /// </returns>
    protected async Task<(IEnumerable<SigningKey>? trustedSigningKeys, IEnumerable<string>? allowedAlgorithms)>
    GetSignatureValidationParametersFromIssuerAsync(XmlTraverser source, NameId? issuer, Ct ct)
    {
        var trustedSigningKeys = TrustedSigningKeys;
        var allowedAlgorithms = AllowedAlgorithms;
        if (source.HasName(SamlConstants.Elements.Signature, SignedXml.XmlDsigNamespaceUrl))
        {
            if (issuer == null)
            {
                source.Errors.Add(new(ErrorReason.MissingElement, SamlConstants.Elements.Issuer, source.CurrentNode,
                    "A signature was found, but there was no Issuer specified. See profile spec 4.1.4.1, 4.1.4.2, 4.4.4.2"));
            }
            else if (EntityResolver is { } resolver)
            {
                var entity = await resolver(issuer.Value, ct);
                trustedSigningKeys = entity?.SigningKeys;
                allowedAlgorithms = entity?.AllowedAlgorithms;
            }
        }

        return (trustedSigningKeys, allowedAlgorithms);
    }

    /// <summary>
    /// Call the supplied error inspector callback if there are errors.
    /// </summary>
    /// <typeparam name="TData">Type of data being handled</typeparam>
    /// <param name="errorInspector">Error inspector callback</param>
    /// <param name="data">The data</param>
    /// <param name="source">Source xml</param>
    protected static void CallErrorInspector<TData>(
        Action<ReadErrorInspectorContext<TData>>? errorInspector,
        TData data,
        XmlTraverser source)
    {
        if (errorInspector != null && source.Errors.Count != 0)
        {
            var context = new ReadErrorInspectorContext<TData>()
            {
                Data = data,
                Errors = source.Errors,
                XmlSource = source.RootNode
            };

            errorInspector(context);
        }
    }
}
