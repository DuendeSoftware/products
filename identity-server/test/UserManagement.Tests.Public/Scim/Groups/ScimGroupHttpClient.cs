// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Duende.Platform.UserManagement.Scim.Groups;

/// <summary>
/// An <see cref="HttpClient"/> subclass with convenience methods for calling SCIM
/// Group endpoints. Every interaction goes through the HTTP pipeline.
/// </summary>
public sealed class ScimGroupHttpClient : HttpClient
{
    public const string ScimContentType = "application/scim+json";
    public const string GroupSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string PatchOpSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    public const string SearchRequestSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:SearchRequest";
    public const string GroupsRoute = "/scim/Groups";

    public string Route { get; set; }

    public ScimGroupHttpClient(HttpMessageHandler handler, Uri baseAddress)
        : this(handler, baseAddress, GroupsRoute) { }

    public ScimGroupHttpClient(HttpMessageHandler handler, Uri baseAddress, string route)
        : base(handler)
    {
        BaseAddress = baseAddress;
        Route = route;
    }

    /// <summary>
    /// Sets the Authorization header to a Bearer token for all subsequent requests.
    /// </summary>
    public void SetBearerToken(string token) =>
        DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Clears the Authorization header so subsequent requests are unauthenticated.
    /// </summary>
    public void ClearBearerToken() =>
        DefaultRequestHeaders.Authorization = null;

    public static StringContent ScimJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, ScimContentType);
    }

    public async Task<(HttpResponseMessage Response, JsonDocument Body)> CreateGroupAsync(
        string displayName, string[]? memberIds = null)
    {
        object payload = memberIds is { Length: > 0 }
            ? new
            {
                schemas = new[] { GroupSchemaUrn },
                displayName,
                members = memberIds.Select(id => new { value = id }).ToArray()
            }
            : new { schemas = new[] { GroupSchemaUrn }, displayName };

        var response = await PostAsync(Route, ScimJsonContent(payload));
        var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonDocument.ParseAsync(stream);
        return (response, body);
    }

    public static string GetGroupId(JsonDocument doc) =>
        doc.RootElement.GetProperty("id").GetString()!;

    public static string GetETag(HttpResponseMessage response) =>
        response.Headers.ETag!.ToString();
}
