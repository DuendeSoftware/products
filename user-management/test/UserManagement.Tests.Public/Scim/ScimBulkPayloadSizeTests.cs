// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimBulkPayloadSizeTests(ITestOutputHelper output, WebServerFixture serverFixture)
{
    private const string UserName = "bulk-payload-limit-jos\u00e9";
    private static readonly JsonSerializerOptions UnescapedJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public async Task content_length_body_exactly_at_the_limit_is_accepted()
    {
        var payload = CreatePayload(UserName);
        await using var fixture = CreateFixture(payload.Length);
        await fixture.InitializeAsync();

        using var response = await SendAsync(fixture.Client, payload, chunked: false);

        await AssertBulkOperationCreatedAsync(response);
    }

    [Fact]
    public async Task chunked_body_exactly_at_the_limit_is_accepted()
    {
        var payload = CreatePayload(UserName);
        await using var fixture = CreateFixture(payload.Length);
        await fixture.InitializeAsync();

        using var response = await SendAsync(fixture.Client, payload, chunked: true);

        await AssertBulkOperationCreatedAsync(response);
    }

    [Fact]
    public async Task chunked_body_one_byte_over_the_limit_returns_scim_413_without_executing_operations()
    {
        var payload = CreatePayload(UserName);
        await using var fixture = CreateFixture(payload.Length - 1);
        await fixture.InitializeAsync();

        using var response = await SendAsync(fixture.Client, payload, chunked: true);

        await AssertScimPayloadTooLargeAsync(response, payload.Length - 1);
        var (createResponse, _) = await fixture.Client.CreateUserAsync(UserName);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task chunked_trailing_whitespace_above_the_limit_returns_scim_413()
    {
        var payload = CreatePayload(UserName);
        var payloadWithTrailingWhitespace = payload.Concat([(byte)' ']).ToArray();
        await using var fixture = CreateFixture(payload.Length);
        await fixture.InitializeAsync();

        using var response = await SendAsync(
            fixture.Client,
            payloadWithTrailingWhitespace,
            chunked: true);

        await AssertScimPayloadTooLargeAsync(response, payload.Length);
    }

    [Fact]
    public async Task chunked_utf16_body_exactly_at_the_limit_is_accepted()
    {
        var utf8Payload = CreatePayload(UserName);
        var payload = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, utf8Payload);
        await using var fixture = CreateFixture(payload.Length);
        await fixture.InitializeAsync();

        using var response = await SendAsync(
            fixture.Client,
            payload,
            chunked: true,
            charset: Encoding.Unicode.WebName,
            route: ScimHttpClient.BulkRoute);

        await AssertBulkOperationCreatedAsync(response);
    }

    [Fact]
    public async Task chunked_utf16_body_over_the_raw_byte_limit_returns_scim_413()
    {
        var utf8Payload = CreatePayload(UserName);
        var payload = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, utf8Payload);
        await using var fixture = CreateFixture(utf8Payload.Length);
        await fixture.InitializeAsync();

        using var response = await SendAsync(
            fixture.Client,
            payload,
            chunked: true,
            charset: Encoding.Unicode.WebName,
            route: ScimHttpClient.BulkRoute);

        await AssertScimPayloadTooLargeAsync(response, utf8Payload.Length);
    }

    [Fact]
    public async Task oversized_declared_length_is_rejected_before_json_binding()
    {
        var payload = Encoding.UTF8.GetBytes("not-json");
        await using var fixture = CreateFixture(payload.Length - 1);
        await fixture.InitializeAsync();

        using var response = await SendAsync(fixture.Client, payload, chunked: false);

        await AssertScimPayloadTooLargeAsync(response, payload.Length - 1);
    }

    [Fact]
    public async Task non_bulk_request_is_not_limited_by_the_bulk_payload_setting()
    {
        await using var fixture = CreateFixture(1);
        await fixture.InitializeAsync();

        var (response, _) = await fixture.Client.CreateUserAsync(new string('x', 100));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task custom_bulk_route_enforces_the_payload_limit()
    {
        const string customRoute = "/custom/Bulk";
        var payload = CreatePayload(UserName);
        await using var fixture = CreateFixture(payload.Length - 1);
        fixture.ConfigureScimOptions = options => options.BulkRoute = customRoute;
        await fixture.InitializeAsync();

        using var response = await SendAsync(
            fixture.Client,
            payload,
            chunked: true,
            charset: null,
            route: customRoute);

        await AssertScimPayloadTooLargeAsync(response, payload.Length - 1);
    }

    [Fact]
    public async Task authorization_runs_before_payload_limit_enforcement()
    {
        var payload = CreatePayload(UserName);
        await using var fixture = CreateFixture(payload.Length - 1);
        await fixture.InitializeAsync();
        fixture.Client.ClearBearerToken();

        using var response = await SendAsync(fixture.Client, payload, chunked: false);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private ScimFixture CreateFixture(int maxPayloadSize) =>
        new(output, serverFixture)
        {
            ConfigureScimCapabilities = options => options.MaxBulkPayloadSize = maxPayloadSize
        };

    private static byte[] CreatePayload(string userName)
    {
        var payload = new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.UserSchemaUrn },
                        userName
                    }
                }
            }
        };

        return JsonSerializer.SerializeToUtf8Bytes(payload, UnescapedJsonOptions);
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        byte[] payload,
        bool chunked) =>
        SendAsync(client, payload, chunked, null, ScimHttpClient.BulkRoute);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        byte[] payload,
        bool chunked,
        string? charset,
        string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = chunked
                ? new UnknownLengthContent(payload)
                : new ByteArrayContent(payload)
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue(ScimHttpClient.ScimContentType);
        request.Content.Headers.ContentType.CharSet = charset;
        request.Headers.TransferEncodingChunked = chunked;

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertScimPayloadTooLargeAsync(
        HttpResponseMessage response,
        int maxPayloadSize)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
        using var body = await JsonDocument.ParseAsync(
            utf8Json: await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );

        body.RootElement.GetProperty("status").GetString()
            .ShouldBe("413");

        body.RootElement.GetProperty("detail").GetString()
            .ShouldBe($"The size of the bulk operation exceeds the maxPayloadSize ({maxPayloadSize}).");
    }

    private static async Task AssertBulkOperationCreatedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(
            utf8Json: await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken
        );

        body.RootElement.GetProperty("Operations")[0]
            .GetProperty("status")
            .GetString()
            .ShouldBe("201");
    }

    private sealed class UnknownLengthContent(byte[] payload) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) =>
            stream.WriteAsync(payload, TestContext.Current.CancellationToken).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
