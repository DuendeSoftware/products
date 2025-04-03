// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Blazor;
using Duende.Bff.Blazor.Client;
using Duende.Bff.EntityFramework;
using Duende.Bff.Yarp;
using PublicApiGenerator;

namespace Bff.Tests;

public class PublicApiVerificationTests
{

    [Fact]
    public async Task VerifyPublicApi_Bff()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(BffBuilder).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }

    [Fact]
    public async Task VerifyPublicApi_Bff_Yarp()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
#pragma warning disable CS0618 // Type or member is obsolete
        var publicApi = typeof(AccessTokenRequestTransform).Assembly.GeneratePublicApi(apiGeneratorOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }

    [Fact]
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


    [Fact]
    public async Task VerifyPublicApi_Bff_Blazor_Client()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
#pragma warning disable CS0618 // Type or member is obsolete
        var publicApi = typeof(AntiforgeryHandler).Assembly.GeneratePublicApi(apiGeneratorOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }

}
