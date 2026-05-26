// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;

namespace Duende.Platform.UserManagement.Scim;

/// <summary>
/// An <see cref="HttpClient"/> subclass with convenience methods for calling SCIM
/// endpoints. Every interaction with the server goes through the HTTP pipeline,
/// making it explicit in tests that these are real HTTP requests.
/// </summary>
public sealed class ScimHttpClient : HttpClient
{
    public const string ScimContentType = "application/scim+json";
    public const string UserSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string GroupSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string ListResponseSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    public const string ErrorSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:Error";
    public const string PatchOpSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    public const string SearchRequestSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:SearchRequest";
    public const string BulkRequestSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:BulkRequest";
    public const string BulkResponseSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:BulkResponse";
    public const string UsersRoute = "/scim/Users";
    public const string BulkRoute = "/scim/Bulk";

    /// <summary>
    /// The route prefix used by this client instance. Defaults to <see cref="UsersRoute"/>.
    /// </summary>
    public string Route { get; set; }

    public ScimHttpClient(HttpMessageHandler handler, Uri baseAddress)
        : this(handler, baseAddress, UsersRoute)
    {
    }

    public ScimHttpClient(HttpMessageHandler handler, Uri baseAddress, string route)
        : base(handler)
    {
        BaseAddress = baseAddress;
        Route = route;
    }

    /// <summary>
    /// Builds a <see cref="StringContent"/> with the SCIM JSON content type.
    /// </summary>
    public static StringContent ScimJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, ScimContentType);
    }

    /// <summary>
    /// Creates a SCIM user via <c>POST /scim/Users</c> and returns the raw response
    /// together with the parsed JSON body.
    /// </summary>
    public async Task<(HttpResponseMessage Response, JsonDocument Body)> CreateUserAsync(
        string userName,
        string? externalId,
        object? additionalAttributes)
    {
        object payload;

        if (additionalAttributes is not null)
        {
            // Serialize the anonymous/typed object to a dictionary, then merge in schemas + userName + externalId
            var json = JsonSerializer.Serialize(additionalAttributes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
            dict["schemas"] = new[] { UserSchemaUrn };
            dict["userName"] = userName;
            if (externalId is not null)
            {
                dict["externalId"] = externalId;
            }

            payload = dict;
        }
        else if (externalId is not null)
        {
            payload = new { schemas = new[] { UserSchemaUrn }, userName, externalId };
        }
        else
        {
            payload = new { schemas = new[] { UserSchemaUrn }, userName };
        }

        var response = await PostAsync(Route, ScimJsonContent(payload));
        var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonDocument.ParseAsync(stream);
        return (response, body);
    }

    /// <summary>
    /// Creates a SCIM user via <c>POST /scim/Users</c> with only a userName.
    /// </summary>
    public Task<(HttpResponseMessage Response, JsonDocument Body)> CreateUserAsync(string userName) =>
        CreateUserAsync(userName, null, null);

    /// <summary>
    /// Creates a SCIM user via <c>POST /scim/Users</c> with a userName and externalId.
    /// </summary>
    public Task<(HttpResponseMessage Response, JsonDocument Body)> CreateUserAsync(
        string userName, string externalId) =>
        CreateUserAsync(userName, externalId, null);

    /// <summary>
    /// Creates a SCIM user via <c>POST /scim/Users</c> with a userName and additional attributes.
    /// </summary>
    public Task<(HttpResponseMessage Response, JsonDocument Body)> CreateUserAsync(
        string userName, object additionalAttributes) =>
        CreateUserAsync(userName, null, additionalAttributes);

    /// <summary>
    /// Creates a SCIM user via <c>POST /scim/Users</c> with a userName and password,
    /// and returns the raw response together with the parsed JSON body.
    /// </summary>
    public async Task<(HttpResponseMessage Response, JsonDocument Body)> CreateUserWithPasswordAsync(
        string userName,
        string password)
    {
        var payload = new { schemas = new[] { UserSchemaUrn }, userName, password };
        var response = await PostAsync(Route, ScimJsonContent(payload));
        var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonDocument.ParseAsync(stream);
        return (response, body);
    }

    /// <summary>Extracts the user <c>id</c> from a SCIM user JSON response.</summary>
    public static string GetUserId(JsonDocument doc) =>
        doc.RootElement.GetProperty("id").GetString()!;

    /// <summary>Extracts the ETag header value from a response (e.g. <c>W/"1"</c>).</summary>
    public static string GetETag(HttpResponseMessage response) =>
        response.Headers.ETag!.ToString();

    /// <summary>
    /// Sends a bulk request to <c>POST /scim/Bulk</c> and returns the response and parsed body.
    /// </summary>
    public async Task<(HttpResponseMessage Response, JsonDocument Body)> BulkAsync(object payload)
    {
        var response = await PostAsync(BulkRoute, ScimJsonContent(payload));
        var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonDocument.ParseAsync(stream);
        return (response, body);
    }
}
