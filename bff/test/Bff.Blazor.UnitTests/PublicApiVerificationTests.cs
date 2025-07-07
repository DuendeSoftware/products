// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor;
using PublicApiGenerator;

namespace Bff.Tests;

public class PublicApiVerificationTests
{
    [Fact]
    public async Task VerifyPublicApi_Bff_Blazor()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(BffBlazorServerOptions).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }
}
