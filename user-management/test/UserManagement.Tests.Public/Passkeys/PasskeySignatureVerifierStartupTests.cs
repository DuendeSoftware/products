// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class PasskeySignatureVerifierStartupTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Host_start_rejects_built_in_algorithm_registered_through_builder()
    {
        var builder = CreateBuilder();
        _ = builder.Services.AddUserManagementInternal(StorageInstanceId.Default, users =>
            users.Authentication(authentication =>
            {
                _ = authentication.AddPasskeySignatureVerifier<TestVerifiers.Es256SignatureVerifier>();
            }));

        await AssertHostStartRejectsAlgorithmAsync(builder, CoseAlgorithms.Es256);
    }

    [Fact]
    public async Task Host_start_rejects_built_in_algorithm_registered_before_user_management()
    {
        var builder = CreateBuilder();
        _ = builder.Services.AddSingleton<ISignatureVerifier, TestVerifiers.Es256SignatureVerifier>();
        _ = builder.Services.AddUserManagementInternal(StorageInstanceId.Default, users =>
            users.Authentication(_ => { }));

        await AssertHostStartRejectsAlgorithmAsync(builder, CoseAlgorithms.Es256);
    }

    [Fact]
    public async Task Host_start_rejects_duplicate_algorithm_excluded_from_supported_algorithms()
    {
        var builder = CreateBuilder();
        _ = builder.Services.AddUserManagementInternal(StorageInstanceId.Default, users =>
            users.Authentication(authentication =>
            {
                _ = authentication.Configure(options => options.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Rs256]);
                _ = authentication.AddPasskeySignatureVerifier<TestVerifiers.Es256SignatureVerifier>();
            }));

        await AssertHostStartRejectsAlgorithmAsync(builder, CoseAlgorithms.Es256);
    }

    [Fact]
    public async Task Host_start_rejects_duplicate_custom_algorithm_registered_after_user_management()
    {
        var builder = CreateBuilder();
        _ = builder.Services.AddUserManagementInternal(StorageInstanceId.Default, users =>
            users.Authentication(authentication =>
            {
                _ = authentication.AddPasskeySignatureVerifier<TestVerifiers.EdDsaSignatureVerifier>();
            }));
        _ = builder.Services.AddSingleton<ISignatureVerifier>(new TestVerifiers.EdDsaSignatureVerifier());

        await AssertHostStartRejectsAlgorithmAsync(builder, -8);
    }

    private async Task AssertHostStartRejectsAlgorithmAsync(WebApplicationBuilder builder, int algorithm)
    {
        await using var app = builder.Build();

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync(_ct));

        exception.OptionsType.ShouldBe(typeof(UserAuthenticationOptions));
        exception.Failures.ShouldBe([$"Multiple passkey signature verifiers are registered for COSE algorithm '{algorithm}'."]);
    }

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
        _ = builder.Services.AddStorageInternal(storage => storage.AddSqliteInMemory());
        return builder;
    }

    // Registration-only test verifiers: expose algorithm IDs but never accept signatures.
    private sealed class TestVerifiers
    {
        internal sealed class Es256SignatureVerifier : ISignatureVerifier
        {
            public int Algorithm => CoseAlgorithms.Es256;
            public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => false;
        }

        internal sealed class EdDsaSignatureVerifier : ISignatureVerifier
        {
            public int Algorithm => -8;
            public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => false;
        }
    }
}
