// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Duende.IdentityServer.Licensing.v2;
using Duende.IdentityServer.Models;
using FluentAssertions;
using IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace IntegrationTests.Hosting;

// The licensing integration tests can't be run in parallel, because they copy license files to the directory 
// where the mock identity server pipeline is executing. If this was done in parallel with other tests, you could
// get license failures during test execution.
[CollectionDefinition("Licensing", DisableParallelization = true)]
public class LicenseTestCollection;

[Collection("Licensing")]
public class LicenseTests : IDisposable
{
    private string client_id = "client";
    private string client_secret = "secret";
    private string scope_name = "api";

    private IdentityServerPipeline _mockPipeline = new();

    public LicenseTests()
    {
        _mockPipeline.Clients.Add(new Client
        {
            ClientId = client_id,
            ClientSecrets = [new Secret(client_secret.Sha256())],
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = ["api"],
        });
        _mockPipeline.ApiScopes = [new ApiScope(scope_name)];
    }

    public void Dispose()
    {
        // Some of our tests involve copying test license files so that the pipeline will read them.
        // This should ensure that they are cleanup up after each test.
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var path1 = Path.Combine(contentRoot, "Duende_License.key");
        if (File.Exists(path1))
        {
            File.Delete(path1);
        }
        var path2 = Path.Combine(contentRoot, "Duende_IdentityServer_License.key");
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
    }
    
    [Fact]
    public async Task unlicensed_warnings_are_logged()
    {
        var threshold = 5u;
        _mockPipeline.OnPostConfigure += builder =>
        {
            var counter = builder.ApplicationServices.GetRequiredService<IProtocolRequestCounter>() as ProtocolRequestCounter;
            counter.Threshold = threshold;
        };
        _mockPipeline.Initialize(enableLogging: true);
        
        // The actual protocol parameters aren't the point of this test, this could be any protocol request 
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);
        
        for (int i = 0; i < threshold + 1; i++)
        {
            await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        }

        _mockPipeline.MockLogger.LogMessages.Should().Contain(
            $"IdentityServer has handled {threshold + 1} protocol requests without a license. In future versions, unlicensed IdentityServer instances will shut down after {threshold} protocol requests. Please contact sales to obtain a license. If you are running in a test environment, please use a test license");
    }

    [Theory]
    [InlineData("6677-starter-standard", "Duende_License.key")]
    [InlineData("6677-starter-standard","Duende_IdentityServer_License.key")]
    [InlineData("6678-business-standard", "Duende_License.key")]
    [InlineData("6678-business-standard", "Duende_IdentityServer_License.key")]
    [InlineData("6680-starter-standard-added-key-management-feature", "Duende_License.key")]
    [InlineData("6680-starter-standard-added-key-management-feature", "Duende_IdentityServer_License.key")]
    [InlineData("6681-business-standard-added-dynamic-providers-feature", "Duende_License.key")]
    [InlineData("6681-business-standard-added-dynamic-providers-feature", "Duende_IdentityServer_License.key")]
    [InlineData("6685-enterprise-standard", "Duende_License.key")]
    [InlineData("6685-enterprise-standard", "Duende_IdentityServer_License.key")]
    [InlineData("6703-community", "Duende_License.key")]
    [InlineData("6703-community", "Duende_IdentityServer_License.key")]
    public async Task expired_license_warnings_are_logged(string licenseFileName, string destinationFileName)
    {
        // Copy a test license to the file system where the mock pipeline will see it
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var sourceFileName = Path.Combine("TestLicenses", licenseFileName);
        var src = Path.Combine(contentRoot, sourceFileName);
        var dest = Path.Combine(contentRoot, destinationFileName);
        File.Copy(src, dest, true);
        
        // Set the time to be after the license expiration
        var timeProvider = new FakeTimeProvider();
        _mockPipeline.OnPreConfigureServices += collection => collection.AddSingleton<TimeProvider>(timeProvider);
        _mockPipeline.Initialize(enableLogging: true);
        var testLicenseExpiration = new DateTime(2024, 11, 15);
        var afterExpiration = testLicenseExpiration + TimeSpan.FromDays(1);
        timeProvider.SetUtcNow(afterExpiration);

        // Make any protocol request
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);
        await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        
        // Expect a warning because the license is expired
        _mockPipeline.MockLogger.LogMessages.Should().Contain("Your license expired on 2024-11-15. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }

    [Theory]
    [InlineData("6682-starter-redist", "Duende_License.key")]
    [InlineData("6682-starter-redist", "Duende_IdentityServer_License.key")]
    [InlineData("6683-business-redist", "Duende_License.key")]
    [InlineData("6683-business-redist", "Duende_IdentityServer_License.key")]
    [InlineData("6684-enterprise-redist", "Duende_License.key")]
    [InlineData("6684-enterprise-redist", "Duende_IdentityServer_License.key")]
    public async Task expired_redist_licenses_do_not_log_warnings(string licenseFileName, string destinationFileName)
    {
        // Copy a test license to the file system where the mock pipeline will see it
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var sourceFileName = Path.Combine("TestLicenses", licenseFileName);
        var src = Path.Combine(contentRoot, sourceFileName);
        var dest = Path.Combine(contentRoot, destinationFileName);
        File.Copy(src, dest, true);
        
        // Set the time to be after the license expiration
        var timeProvider = new FakeTimeProvider();
        _mockPipeline.OnPreConfigureServices += collection => collection.AddSingleton<TimeProvider>(timeProvider);
        _mockPipeline.Initialize(enableLogging: true);
        var testLicenseExpiration = new DateTime(2024, 11, 15);
        var afterExpiration = testLicenseExpiration + TimeSpan.FromDays(1);
        timeProvider.SetUtcNow(afterExpiration);

        // Make any protocol request
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);
        await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        
        // Expect no warning because the license is a redistribution license
        _mockPipeline.MockLogger.LogMessages.Should().NotContain("Your license expired on 2024-11-15. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }
    
    [Theory]
    [InlineData("6677-starter-standard", "Duende_License.key")]
    [InlineData("6678-business-standard", "Duende_License.key")]
    [InlineData("6678-business-standard", "Duende_IdentityServer_License.key")]
    [InlineData("6680-starter-standard-added-key-management-feature", "Duende_License.key")]
    [InlineData("6680-starter-standard-added-key-management-feature", "Duende_IdentityServer_License.key")]
    [InlineData("6681-business-standard-added-dynamic-providers-feature", "Duende_License.key")]
    [InlineData("6681-business-standard-added-dynamic-providers-feature", "Duende_IdentityServer_License.key")]
    [InlineData("6685-enterprise-standard", "Duende_License.key")]
    [InlineData("6685-enterprise-standard", "Duende_IdentityServer_License.key")]
    [InlineData("6703-community", "Duende_License.key")]
    [InlineData("6703-community", "Duende_IdentityServer_License.key")]
    public async Task nonexpired_license_warnings_are_not_logged(string licenseFileName, string destinationFileName)
    {
        // Copy a test license to the file system where the mock pipeline will see it
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var sourceFileName = Path.Combine("TestLicenses", licenseFileName);
        var src = Path.Combine(contentRoot, sourceFileName);
        var dest = Path.Combine(contentRoot, destinationFileName);
        File.Copy(src, dest, true);
        
        // Set the time to be before the license expired
        var timeProvider = new FakeTimeProvider();
        _mockPipeline.OnPreConfigureServices += collection => collection.AddSingleton<TimeProvider>(timeProvider);
        _mockPipeline.Initialize(enableLogging: true);
        var testLicenseExpiration = new DateTime(2024, 11, 15);
        var beforeExpiration = testLicenseExpiration - TimeSpan.FromDays(1);
        timeProvider.SetUtcNow(beforeExpiration);

        // Make any protocol request
        var data = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", client_id },
            { "client_secret", client_secret },
            { "scope", scope_name },
        };
        var form = new FormUrlEncodedContent(data);
        await _mockPipeline.BackChannelClient.PostAsync(IdentityServerPipeline.TokenEndpoint, form);
        
        // Expect no warning because the license is not expired
        _mockPipeline.MockLogger.LogMessages.Should().NotContain("Your license expired on 2024-11-15. You are required to obtain a new license. In a future version of IdentityServer, expired licenses will stop the server after 90 days.");
    }
    
    
}