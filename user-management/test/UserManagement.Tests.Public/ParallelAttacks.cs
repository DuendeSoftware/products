// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

/// <summary>
/// Validates that the OTP authentication workflow correctly prevents parallel
/// authentication with the same OTP. Uses in-memory storage only — the underlying
/// store concurrency is proven across all databases in Storage.Tests (see
/// ConcurrentCreateReturnsAlreadyExists and ConcurrentUpdateReturnsUnexpectedVersion).
/// </summary>
[Trait("PasswordHashing", "True")]
public sealed class ParallelAttacks : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _serviceProvider = await UsersServiceProviderFactory.CreateAsync();

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Cannot_authenticate_by_otp()
    {
        // arrange
        if (Environment.GetEnvironmentVariable("CI")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            Environment.ProcessorCount.ShouldBeGreaterThanOrEqualTo(16);
        }

        var auth = _serviceProvider.GetRequiredService<IOtpAuthenticator>();

        var sendResult = (await auth.TrySendOtpAsync(TestData.CreateOtpAddress(), _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();

        var otpSender = _serviceProvider.GetRequiredService<FakeOtpSender>();
        otpSender.Calls.ShouldNotBeEmpty();
        var otp = otpSender.Calls.First().Otp;

        // act
        var attempts = await Task.WhenAll(Enumerable.Range(1, Environment.ProcessorCount)
            .Select(_ => Task.Run(() => auth.TryAuthenticateAsync(otp, sendResult.Token.Value, _ct), _ct))
            .ToArray());

        // assert
        _ = attempts.Where(a => a is OtpAuthenticationResult.Success).ShouldHaveSingleItem();
    }
}
