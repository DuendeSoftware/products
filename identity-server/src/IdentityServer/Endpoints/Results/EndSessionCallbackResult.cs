// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Models the result of end session callback
/// </summary>
public class EndSessionCallbackResult : EndpointResult<EndSessionCallbackResult>
{
    /// <summary>
    /// The result
    /// </summary>
    public EndSessionCallbackValidationResult Result { get; }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="result"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public EndSessionCallbackResult(EndSessionCallbackValidationResult result) => Result = result ?? throw new ArgumentNullException(nameof(result));
}

internal class EndSessionCallbackHttpWriter : IHttpResponseWriter<EndSessionCallbackResult>
{
    public EndSessionCallbackHttpWriter(IdentityServerOptions options, ILogger<EndSessionCallbackHttpWriter> logger)
    {
        _options = options;
        _logger = logger;
    }

    private readonly IdentityServerOptions _options;
    private readonly ILogger<EndSessionCallbackHttpWriter> _logger;

    public async Task WriteHttpResponse(EndSessionCallbackResult result, HttpContext context)
    {
        if (result.Result.IsError)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
        else
        {
            context.Response.SetNoCache();
            AddCspHeaders(result, context);

            var html = GetHtml(result);
            await context.Response.WriteHtmlAsync(html);
        }
    }

    private void AddCspHeaders(EndSessionCallbackResult result, HttpContext context)
    {
        if (_options.Authentication.RequireCspFrameSrcForSignout)
        {
            var sb = new StringBuilder();
            var origins = result.Result.FrontChannelLogoutUrls?.Select(x => x.GetOrigin()) ?? [];
            origins = origins.Concat(result.Result.SamlFrontChannelLogouts.Select(x => x.Message.Destination.GetOrigin()));

            // When SAML SPs receive a front-channel LogoutRequest in an iframe, they respond
            // with a redirect back to the IdP's SLO endpoint. Allow 'self' so the browser
            // permits that redirect within the iframe.
            if (result.Result.SamlFrontChannelLogouts.Any())
            {
                origins = origins.Append("'self'");
            }

            foreach (var origin in origins.Distinct())
            {
                sb.Append(origin);
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
            }

            // the hash matches the embedded style element being used below
            context.Response.AddStyleCspHeaders(_options.Csp, IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle, sb.ToString());
        }
    }

    private string GetHtml(EndSessionCallbackResult result)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><style>iframe{{display:none;width:0;height:0;}}</style><body>");

        if (result.Result.FrontChannelLogoutUrls != null)
        {
            foreach (var url in result.Result.FrontChannelLogoutUrls)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "<iframe loading='eager' allow='' src='{0}'></iframe>", HtmlEncoder.Default.Encode(url));
                sb.AppendLine();
            }
        }

        if (result.Result.SamlFrontChannelLogouts.Any())
        {
            foreach (var requestContext in result.Result.SamlFrontChannelLogouts)
            {
                var message = requestContext.Message;
                if (message.Binding != SamlConstants.Bindings.HttpRedirect)
                {
                    _logger.LogDebug("Unsupported SAML Binding: {Binding}", message.Binding);
                    continue;
                }

                var queryString = HttpRedirectBinding.GetQueryString(message);
                var separator = message.Destination.Contains('?', StringComparison.Ordinal) ? "&" : "?";
                var redirectUrl = $"{message.Destination}{separator}{queryString.TrimStart('?')}";
                sb.Append(CultureInfo.InvariantCulture, $"<iframe loading='eager' allow='' src='{HtmlEncoder.Default.Encode(redirectUrl)}'></iframe>");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
