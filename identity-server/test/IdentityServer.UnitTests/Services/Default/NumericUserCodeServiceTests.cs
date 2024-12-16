// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using FluentAssertions;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Services.Default;

public class NumericUserCodeGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_should_return_expected_code()
    {
        var sut = new NumericUserCodeGenerator();

        var userCode = await sut.GenerateAsync();
        var userCodeInt = int.Parse(userCode);

        userCodeInt.Should().BeGreaterOrEqualTo(100000000);
        userCodeInt.Should().BeLessOrEqualTo(999999999);
    }
}
