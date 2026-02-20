// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Services.Default;

public class NumericUserCodeGeneratorTests
{
    private readonly CT _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerateAsync_should_return_expected_code()
    {
        var sut = new NumericUserCodeGenerator();

        var userCode = await sut.GenerateAsync(_ct);
        var userCodeInt = int.Parse(userCode);

        userCodeInt.ShouldBeGreaterThanOrEqualTo(100000000);
        userCodeInt.ShouldBeLessThanOrEqualTo(999999999);
    }
}
