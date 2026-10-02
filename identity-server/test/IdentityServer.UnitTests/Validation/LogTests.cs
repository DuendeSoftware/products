// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Testing;

namespace UnitTests.Validation;

public class LogTests
{
    [Fact]
    public void client_not_configured_with_ciba_grant_type_should_render_value_and_preserve_structured_properties()
    {
        var logger = new FakeLogger<LogTests>();
        var details = new { Request = "details" };

        logger.ClientNotConfiguredWithCibaGrantType("client", details);

        var record = logger.Collector.LatestRecord;
        record.Message.ShouldBe("Client client not configured with the CIBA grant type., details: { Request = details }");
        record.GetStructuredStateValue("ClientId").ShouldBe("client");
        record.GetStructuredStateValue("@Details").ShouldBe(details.ToString());
    }

    [Fact]
    public void unexpected_code_verifier_should_render_value_and_preserve_structured_properties()
    {
        var logger = new FakeLogger<LogTests>();
        var details = new { Request = "details" };

        logger.UnexpectedCodeVerifier("verifier", details);

        var record = logger.Collector.LatestRecord;
        record.Message.ShouldBe("Unexpected code_verifier: verifier. This happens when the client is trying to use PKCE, but it is not enabled. Set RequirePkce to true., details: { Request = details }");
        record.GetStructuredStateValue("CodeVerifier").ShouldBe("verifier");
        record.GetStructuredStateValue("@Details").ShouldBe(details.ToString());
    }
}
