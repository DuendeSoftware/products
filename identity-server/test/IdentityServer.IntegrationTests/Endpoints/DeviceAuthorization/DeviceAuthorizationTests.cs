// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.DeviceAuthorization;

public class DeviceAuthorizationTests
{
    private const string Category = "Device authorization endpoint";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public DeviceAuthorizationTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "client1",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.DeviceFlow,
            AllowedScopes = { "openid" }
        });

        _mockPipeline.IdentityScopes.AddRange(new IdentityResource[] {
            new IdentityResources.OpenId()
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task get_should_return_InvalidRequest()
    {
        var response = await _mockPipeline.BackChannelClient.GetAsync(IdentityServerPipeline.DeviceAuthorization);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resultDto = ParseJsonBody<ErrorResultDto>(await response.Content.ReadAsStreamAsync());

        resultDto.ShouldNotBeNull();
        resultDto.error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task wrong_content_type_return_InvalidRequest()
    {
        var form = new Dictionary<string, string>
        {
            {"client_id", Guid.NewGuid().ToString()}
        };
        var response = await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.DeviceAuthorization,
            new StringContent(@"{""client_id"": ""client1""}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resultDto = ParseJsonBody<ErrorResultDto>(await response.Content.ReadAsStreamAsync());

        resultDto.ShouldNotBeNull();
        resultDto.error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task empty_request_should_return_InvalidRequest()
    {
        var response = await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.DeviceAuthorization,
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resultDto = ParseJsonBody<ErrorResultDto>(await response.Content.ReadAsStreamAsync());

        resultDto.ShouldNotBeNull();
        resultDto.error.ShouldBe(OidcConstants.TokenErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task unknown_client_should_return_InvalidClient()
    {
        var form = new Dictionary<string, string>
        {
            {"client_id", "client1"}
        };
        var response = await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.DeviceAuthorization, new FormUrlEncodedContent(form));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var resultDto = ParseJsonBody<ErrorResultDto>(await response.Content.ReadAsStreamAsync());

        resultDto.ShouldNotBeNull();
        resultDto.error.ShouldBe(OidcConstants.TokenErrors.InvalidClient);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task valid_should_return_json()
    {
        var form = new Dictionary<string, string>
        {
            {"client_id", "client1"},
            {"client_secret", "secret" }
        };
        var response = await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.DeviceAuthorization, new FormUrlEncodedContent(form));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");

        var resultDto = ParseJsonBody<ResultDto>(await response.Content.ReadAsStreamAsync());

        resultDto.ShouldNotBeNull();

        resultDto.ShouldNotBeNull();
        resultDto.device_code.ShouldNotBeNull();
        resultDto.user_code.ShouldNotBeNull();
        resultDto.verification_uri.ShouldNotBeNull();
        resultDto.verification_uri_complete.ShouldNotBeNull();
        resultDto.expires_in.ShouldBeGreaterThan(0);
        resultDto.interval.ShouldBeGreaterThan(0);
    }

    private T ParseJsonBody<T>(Stream streamBody)
    {
        streamBody.Position = 0;
        using (var reader = new StreamReader(streamBody))
        {
            var jsonString = reader.ReadToEnd();
            return JsonSerializer.Deserialize<T>(jsonString);
        }
    }

    internal class ResultDto
    {
        public string device_code { get; set; }
        public string user_code { get; set; }
        public string verification_uri { get; set; }
        public string verification_uri_complete { get; set; }
        public int expires_in { get; set; }
        public int interval { get; set; }
    }

    internal class ErrorResultDto
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
}
