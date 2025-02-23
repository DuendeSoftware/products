// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Implements IServerUrls
/// </summary>
public class DefaultServerUrls : IServerUrls
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// ctor
    /// </summary>
    public DefaultServerUrls(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Origin
    {
        get
        {
            var request = _httpContextAccessor.HttpContext.Request;
            return request.Scheme + "://" + request.Host.ToUriComponent();
        }
        set
        {
            var split = value.Split("://", StringSplitOptions.RemoveEmptyEntries);

            var request = _httpContextAccessor.HttpContext.Request;
            request.Scheme = split.First();
            request.Host = new HostString(split.Last());
        }
    }

    /// <inheritdoc/>
    public string BasePath
    {
        get
        {
            return _httpContextAccessor.HttpContext.Items[Constants.EnvironmentKeys.IdentityServerBasePath] as string;
        }
        set
        {
            _httpContextAccessor.HttpContext.Items[Constants.EnvironmentKeys.IdentityServerBasePath] = value.RemoveTrailingSlash();
        }
    }
}