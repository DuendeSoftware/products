// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Internal.Saml;
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Models;
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
            origins = origins.Concat(result.Result.SamlFrontChannelLogouts.Select(x => x.Destination.OriginalString));
            foreach (var origin in origins.Distinct())
            {
                sb.Append(origin);
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
            }

            if (result.Result.SamlFrontChannelLogouts.Any())
            {
                // the hash matches the embedded style element being used below
                // and the SAML auto-post script hash allows the inline script in the iframe srcdoc
                context.Response.AddStyleAndScriptCspHeaders(
                    _options.Csp,
                    IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle,
                    IdentityServerConstants.ContentSecurityPolicyHashes.SamlAutoPostScript,
                    sb.ToString());
            }
            else
            {
                // the hash matches the embedded style element being used below
                context.Response.AddStyleCspHeaders(_options.Csp, IdentityServerConstants.ContentSecurityPolicyHashes.EndSessionStyle, sb.ToString());
            }
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
            foreach (var samlFrontChannelLogout in result.Result.SamlFrontChannelLogouts)
            {
                switch (samlFrontChannelLogout.SamlBinding)
                {
                    case SamlBinding.HttpPost:
                        var autoPostFormContent = HttpResponseBindings.GenerateAutoPostForm(SamlMessageName.SamlRequest, samlFrontChannelLogout.EncodedContent, samlFrontChannelLogout.Destination, samlFrontChannelLogout.RelayState, includeCsp: true);
                        sb.Append(CultureInfo.InvariantCulture, $"<iframe sandbox='allow-forms allow-scripts allow-same-origin' srcdoc='{HtmlEncoder.Default.Encode(autoPostFormContent)}'></iframe>");
                        break;
                    case SamlBinding.HttpRedirect:
                        sb.Append(CultureInfo.InvariantCulture, $"<iframe loading='eager' allow='' src='{HtmlEncoder.Default.Encode($"{samlFrontChannelLogout.Destination}?{samlFrontChannelLogout.EncodedContent}")}'></iframe>");
                        break;
                    default:
                        _logger.LogDebug("Unknown SAML Binding: {SamlBinding}", samlFrontChannelLogout.SamlBinding);
                        break;
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
