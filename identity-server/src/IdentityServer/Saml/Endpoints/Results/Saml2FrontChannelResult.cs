// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Duende.IdentityServer.Saml.Endpoints.Results;

/// <summary>
/// Result from a Saml2 endpoint that wraps a Saml2 message and should be handled by
/// a front channel binding.
/// </summary>
public class Saml2FrontChannelResult : EndpointResult<Saml2FrontChannelResult>
{
    /// <summary>
    /// Contained Saml2 Message
    /// </summary>
    public OutboundSaml2Message? Message { get; set; }

    /// <summary>
    /// Error message if this result is an error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Entity Id of Sp as received in incoming message, may not be validated.
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    /// The NameID that was generated for the subject in this response. Null when the result is an error.
    /// </summary>
    public NameId? GeneratedNameId { get; set; }
}

/// <summary>
/// Write a Saml2 front channel result to the HttpContext
/// </summary>
internal class Saml2FrontChannelResultHttpWriter(
    TimeProvider timeProvider,
    IMessageStore<ErrorMessage> errorMessageStore,
    IdentityServerOptions identityServerOptions,
    IEnumerable<IFrontChannelBinding> bindings)
    : IHttpResponseWriter<Saml2FrontChannelResult>
{
    /// <inheritdoc/>
    public async Task WriteHttpResponse(Saml2FrontChannelResult result, HttpContext context)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            var errorMessage = new ErrorMessage()
            {
                Error = "Saml2 error",
                ErrorDescription = result.Error,
                ClientId = result.SpEntityId,
                RequestId = context.TraceIdentifier,
                ActivityId = System.Diagnostics.Activity.Current?.Id
            };

            var message = new Message<ErrorMessage>(errorMessage, timeProvider.GetUtcNow().UtcDateTime);
            var id = await errorMessageStore.WriteAsync(message, context.RequestAborted);

            var errorUrl = identityServerOptions.UserInteraction.ErrorUrl;

            var url = QueryHelpers.AddQueryString(errorUrl, identityServerOptions.UserInteraction.ErrorIdParameter, id);
            context.Response.Redirect(url);
            return;
        }

        if (result.Message != null)
        {
            var binding = bindings.Single(b => b.Identifier == result.Message.Binding);

            await binding.BindAsync(context.Response, result.Message);

            return;
        }

        throw new InvalidOperationException("Saml2FrontChannelResponse contains no properties to take action on");
    }
}
