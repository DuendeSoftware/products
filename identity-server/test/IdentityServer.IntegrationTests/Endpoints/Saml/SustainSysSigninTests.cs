// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Web;
using static Duende.IdentityServer.IntegrationTests.Endpoints.Saml.SamlTestHelpers;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

public class SustainSysSigninTests
{
    private const string Category = "SustainSys SAML signin";

    private SustainSysSamlTestFixture Fixture = new();

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_saml_signin_request()
    {
        // Arrange
        await Fixture.InitializeAsync();

        await Fixture.LoginUserAtIdentityProvider();

        // Act
        var result = await Fixture.BrowserClient!.GetAsync("/protected-resource");

        // Assert
        var acsResult = await ManuallySubmitSamlFormResponse(result);

        // completing the flow should result in receiving a response from the initial protected resource request
        acsResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acsResponse = await acsResult.Content.ReadAsStringAsync();
        acsResponse.ShouldBe("Protected Resource");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_signed_saml_signin_request()
    {
        // Arrange
        Fixture.GenerateSigningCertificate();
        await Fixture.InitializeAsync();

        await Fixture.LoginUserAtIdentityProvider();

        // Act
        var result = await Fixture.BrowserClient!.GetAsync("/protected-resource");

        // Assert
        var acsResult = await ManuallySubmitSamlFormResponse(result);

        // completing the flow should result in receiving a response from the initial protected resource request
        acsResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acsResponse = await acsResult.Content.ReadAsStringAsync();
        acsResponse.ShouldBe("Protected Resource");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task can_initiate_signin_request_for_encrypted_assertions()
    {
        // Arrange
        Fixture.GenerateSigningCertificate();
        Fixture.RequireEncryptedAssertions();
        await Fixture.InitializeAsync();

        await Fixture.LoginUserAtIdentityProvider();

        // Act
        var result = await Fixture.BrowserClient!.GetAsync("/protected-resource");

        // Assert
        var acsResult = await ManuallySubmitSamlFormResponse(result);

        // completing the flow should result in receiving a response from the initial protected resource request
        acsResult.StatusCode.ShouldBe(HttpStatusCode.OK);
        var acsResponse = await acsResult.Content.ReadAsStringAsync();
        acsResponse.ShouldBe("Protected Resource");

        // verify subject id was also parsed correctly after decrypting assertions
        var userInfo = await Fixture.BrowserClient!.GetAsync("/user-name-identifier");
        var userInfoResponse = await userInfo.Content.ReadAsStringAsync();
        userInfoResponse.ShouldBe("user-id");
    }

    private async Task<HttpResponseMessage> ManuallySubmitSamlFormResponse(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // since HttpClient doesn't support JavaScript, we need to extra the content from the auto form post and manually
        // complete the callback to the Service Provider's ACS URL the same way a user in a browser with JavaScript disabled
        // would have to manually submit the form
        var (samlResponse, relayState, acsUrl) = await ExtractSamlResponse(response, CancellationToken.None);
        var formData = new Dictionary<string, string> { { "SAMLResponse", ConvertToBase64Encoded(samlResponse) } };
        if (!string.IsNullOrEmpty(relayState))
        {
            formData.Add("RelayState", HttpUtility.UrlEncode(relayState));
        }
        using var formContent = new FormUrlEncodedContent(formData);
        var acsResult = await Fixture.BrowserClient!.PostAsync(acsUrl, formContent, CancellationToken.None);

        return acsResult;
    }
}
