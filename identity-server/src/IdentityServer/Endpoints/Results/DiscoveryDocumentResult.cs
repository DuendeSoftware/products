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
    private Dictionary<string, object> _entries = new();
    private readonly bool _isCachingEnabled;

    /// <summary>
    /// Gets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; }

    /// <summary>
    /// Gets the JSON representation of the entries in the discovery document.
    /// </summary>
    /// <value>
    /// A JSON string that represents the entries.
    /// </value>
    public string Json { get; private set; }

    /// <summary>
    /// Gets or sets the collection of entries within the discovery document result.
    /// </summary>
    /// <value>
    /// A dictionary containing the discovery document's entries.
    /// </value>
    public Dictionary<string, object> Entries
    {
        get
        {
            if (_isCachingEnabled)
            {
                throw new InvalidOperationException(
                    "Cannot get Entries when discovery document caching is enabled. Use CustomEntries on DiscoveryOptions instead.");
            }

            return _entries;
        }
        set
        {
            if (_isCachingEnabled)
            {
                throw new InvalidOperationException(
                    "Cannot set Entries when discovery document caching is enabled. Use CustomEntries on DiscoveryOptions instead.");
            }

            _entries = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryDocumentResult" /> class.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="maxAge">The maximum age.</param>
    /// <exception cref="System.ArgumentNullException">entries</exception>
    public DiscoveryDocumentResult(Dictionary<string, object> entries, int? maxAge = null)
    {
        MaxAge = maxAge;
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
        Json = ObjectSerializer.ToString(entries);
    }

    /// <summary>
    /// Represents the result of a discovery document operation.
    /// </summary>
    /// <remarks>
    /// Encapsulates the properties and logic required to represent the discovery document's
    /// data along with optional age-based caching information applicable to the response.
    /// </remarks>
    internal DiscoveryDocumentResult(string json, int? maxAge)
    {
        _isCachingEnabled = true;
        Json = json ?? throw new ArgumentNullException(nameof(json));
        MaxAge = maxAge;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryDocumentResult" /> class.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="isCachingEnabled">Indicates that discovery document caching is enabled, preventing direct access to <see cref="Entries"/>.</param>
    /// <param name="maxAge">The maximum age.</param>
    /// <exception cref="System.ArgumentNullException">entries</exception>
    internal DiscoveryDocumentResult(Dictionary<string, object> entries, bool isCachingEnabled, int? maxAge)
        : this(entries, maxAge) => _isCachingEnabled = isCachingEnabled;
}

internal class DiscoveryDocumentHttpWriter : IHttpResponseWriter<DiscoveryDocumentResult>
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
