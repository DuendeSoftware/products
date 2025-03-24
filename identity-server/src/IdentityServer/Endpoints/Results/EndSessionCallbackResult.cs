// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;

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
    public EndSessionCallbackResult(EndSessionCallbackValidationResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }
}

internal class EndSessionCallbackHttpWriter : IHttpResponseWriter<EndSessionCallbackResult>
{
    public EndSessionCallbackHttpWriter(IdentityServerOptions options)
    {
        _options = options;
    }

    private IdentityServerOptions _options;

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
            var origins = result.Result.FrontChannelLogoutUrls?.Select(x => x.GetOrigin());
            if (origins != null)
            {
                foreach (var origin in origins.Distinct())
                {
                    sb.Append(origin);
                    if (sb.Length > 0) sb.Append(' ');
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
                sb.AppendFormat("<iframe loading='eager' allow='' src='{0}'></iframe>", HtmlEncoder.Default.Encode(url));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
