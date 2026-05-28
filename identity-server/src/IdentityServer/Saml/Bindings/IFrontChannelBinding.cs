// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml.Bindings;

/// <summary>
/// A SAML2 Binding that operates on the front channel, i.e. browser.
/// </summary>
public interface IFrontChannelBinding
{
    /// <summary>
    /// The identifying Uri for this binding.
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Binds a Saml2 message to the http response.
    /// </summary>
    /// <param name="message">Saml2 message</param>
    /// <param name="httpResponse">Http response to bind message to</param>
    /// <returns>Task</returns>
    /// <exception cref="System.ArgumentException">If message properties not properly set</exception>
    Task BindAsync(HttpResponse httpResponse, OutboundSaml2Message message);

    /// <summary>
    /// Unbinds a Saml2 message from an http request.
    /// </summary>
    /// <param name="httpRequest">HttpRequest to unbind from</param>
    /// <param name="entityResolver">Resolves a SAML entity by its entity ID for signature validation</param>
    /// <returns>Saml2 message</returns>
    Task<InboundSaml2Message> UnBindAsync(HttpRequest httpRequest,
        Func<string, Ct, Task<Saml2Entity?>> entityResolver);

    /// <summary>
    /// Can the current binding unbind a Saml message from the request?
    /// </summary>
    /// <param name="httpRequest"></param>
    /// <returns></returns>
    bool CanUnBind(HttpRequest httpRequest);
}

/// <summary>
/// A SAML2 Binding that operates on the front channel, i.e. browser.
/// </summary>
/// <param name="identifier">Identifying Uri for this binding</param>
public abstract class FrontChannelBinding(string identifier) : IFrontChannelBinding
{
    /// <inheritdoc/>
    public string Identifier { get; } = identifier;

    /// <inheritdoc/>
    public abstract bool CanUnBind(HttpRequest httpRequest);

    /// <inheritdoc/>
    public Task BindAsync(HttpResponse httpResponse, OutboundSaml2Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Name))
        {
            throw new ArgumentException("Name property must have value", nameof(message));
        }

        if (message.Xml == null)
        {
            throw new ArgumentException("Xml property must have value", nameof(message));
        }

        return DoBindAsync(httpResponse, message);
    }

    /// <summary>
    /// Binds a Saml2 message to the http response.
    /// </summary>
    /// <param name="message">Saml2 message</param>
    /// <param name="httpResponse">Http response to bind message to</param>
    /// <returns>Task</returns>
    protected abstract Task DoBindAsync(HttpResponse httpResponse, OutboundSaml2Message message);

    /// <inheritdoc />
    public Task<InboundSaml2Message> UnBindAsync(
        HttpRequest httpRequest,
        Func<string, Ct, Task<Saml2Entity?>> entityResolver) =>
        DoUnBindAsync(httpRequest, entityResolver);

    /// <summary>
    /// Unbinds a Saml2 message from an http request.
    /// </summary>
    /// <param name="httpRequest">HttpRequest to unbind from</param>
    /// <param name="entityResolver">Resolves a SAML entity by its entity ID for signature validation</param>
    /// <returns>Saml2 message</returns>
    protected abstract Task<InboundSaml2Message> DoUnBindAsync(
        HttpRequest httpRequest,
        Func<string, Ct, Task<Saml2Entity?>> entityResolver);
}
