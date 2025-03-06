// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for a discovery document
/// </summary>
/// <seealso cref="IEndpointResult" />
public class DiscoveryDocumentResult : EndpointResult<DiscoveryDocumentResult>
{
    /// <summary>
    /// Gets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; }

    /// <summary>
    /// The Entries used to serialize the JSON property
    /// </summary>
    public Dictionary<string, object> Entries { get; set; }

    /// <summary>
    /// The JSON version of the Discovery
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryDocumentResult" /> class.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="maxAge">The maximum age.</param>
    /// <exception cref="System.ArgumentNullException">entries</exception>
    public DiscoveryDocumentResult(Dictionary<string, object> entries, int? maxAge = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        MaxAge = maxAge;
        Entries = entries;

        // serialize in advance of writing this result
        Json = ObjectSerializer.ToString(entries);
    }
}

class DiscoveryDocumentHttpWriter : IHttpResponseWriter<DiscoveryDocumentResult>
{
    /// <inheritdoc/>
    public Task WriteHttpResponse(DiscoveryDocumentResult result, HttpContext context)
    {
        if (result.MaxAge.HasValue && result.MaxAge.Value >= 0)
        {
            context.Response.SetCache(result.MaxAge.Value, "Origin");
        }

        return context.Response.WriteJsonAsync(result.Json);
    }
}