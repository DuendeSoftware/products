// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CS8767
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CA1062

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Internal.Services;

/// <summary>
/// Implements IServerUrls using the current HTTP request.
/// </summary>
internal class DefaultServerUrls : IServerUrls
{
    private const string BasePathKey = "um:ServerBasePath";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultServerUrls"/> class.
    /// </summary>
    public DefaultServerUrls(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

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
        get => _httpContextAccessor.HttpContext.Items[BasePathKey] as string;
        set
        {
            var path = value;
            if (path != null && path.EndsWith('/'))
            {
                path = path[..^1];
            }

            _httpContextAccessor.HttpContext.Items[BasePathKey] = path;
        }
    }
}
