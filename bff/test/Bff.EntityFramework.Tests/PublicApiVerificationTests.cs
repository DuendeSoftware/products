// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.EntityFramework;
using PublicApiGenerator;

namespace Bff.Tests;

public class PublicApiVerificationTests
{
    public async Task VerifyPublicApi_Bff_EntityFramework()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(ISessionDbContext).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }
}
