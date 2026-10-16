// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.Spaces;
using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Authentication.Totp.Internal;
using Duende.UserManagement.Authentication.Totp.Internal.Storage;
using Duende.UserManagement.Internal.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using UserAuthenticators = Duende.UserManagement.Authentication.Internal.UserAuthenticators;
using UserSubjectIdDskV1 = Duende.UserManagement.Authentication.Internal.Storage.UserSubjectIdDskV1;

namespace Duende.Platform.UserManagement;

// Storage pools are already partitioned by space, so a secret enrolled in one space is never found by a read in
// another. To test only the data protection boundary, these tests copy the protected stored value from one space's
// pool into a user in another space's pool and then read it back through the repository.
public sealed class SpacesTotpDataProtectionTests : IAsyncDisposable
{
    private const ulong EnrolmentTimeSeconds = 946684800UL;
    private const string EnrolmentTotp = "795445";

    private static readonly PlainBytesTotpKey Key = PlainBytesTotpKey.DecodeFromBase32("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider? _serviceProvider;
    private SpaceId _spaceA = null!;
    private SpaceId _spaceB = null!;

    public ValueTask DisposeAsync() => _serviceProvider?.DisposeAsync() ?? ValueTask.CompletedTask;

    [Fact]
    public async Task A_totp_secret_enrolled_in_one_space_cannot_be_read_in_another_space()
    {
        var sp = await CreateHostAsync(partitionDataProtection: true);
        var subjectInA = await EnrolTotpAsync(sp, _spaceA);
        var copiedValue = await ReadStoredTotpDeviceAsync(sp, _spaceA, subjectInA);
        var subjectInB = await CreateUserAsync(sp, _spaceB);
        await ReplaceStoredTotpDeviceAsync(sp, _spaceB, subjectInB, copiedValue);

        _ = await Should.ThrowAsync<CryptographicException>(() => ReadAsync(sp, _spaceB, subjectInB));
    }

    [Fact]
    public async Task A_totp_secret_enrolled_in_one_space_is_still_readable_in_that_space()
    {
        var sp = await CreateHostAsync(partitionDataProtection: true);
        var subject = await EnrolTotpAsync(sp, _spaceA);

        var read = await ReadAsync(sp, _spaceA, subject);

        read.TotpDevices[TotpDeviceName.Default].Key.Bytes.ToArray().ShouldBe(Key.Bytes.ToArray());
    }

    [Fact]
    public async Task A_totp_secret_copied_to_another_space_is_readable_when_partitioning_is_off()
    {
        // Control: shows the cross-space copy works, so the partitioned failure comes from data protection.
        var sp = await CreateHostAsync(partitionDataProtection: false);
        var subjectInA = await EnrolTotpAsync(sp, _spaceA);
        var copiedValue = await ReadStoredTotpDeviceAsync(sp, _spaceA, subjectInA);
        var subjectInB = await CreateUserAsync(sp, _spaceB);
        await ReplaceStoredTotpDeviceAsync(sp, _spaceB, subjectInB, copiedValue);

        var read = await ReadAsync(sp, _spaceB, subjectInB);

        read.TotpDevices[TotpDeviceName.Default].Key.Bytes.ToArray().ShouldBe(Key.Bytes.ToArray());
    }

    [Fact]
    public async Task A_totp_secret_protected_with_the_unpartitioned_purpose_chain_is_readable_in_the_default_space()
    {
        var sp = await CreateHostAsync(partitionDataProtection: true);
        var subject = await CreateUserAsync(sp, SpaceId.Default);
        await ReplaceStoredTotpDeviceAsync(sp, SpaceId.Default, subject, ProtectWithUnpartitionedPurposeChain(sp));

        var read = await ReadAsync(sp, SpaceId.Default, subject);

        read.TotpDevices[TotpDeviceName.Default].Key.Bytes.ToArray().ShouldBe(Key.Bytes.ToArray());
    }

    [Fact]
    public async Task A_totp_secret_protected_with_the_unpartitioned_purpose_chain_cannot_be_read_in_a_named_space()
    {
        var sp = await CreateHostAsync(partitionDataProtection: true);
        var subject = await CreateUserAsync(sp, _spaceA);
        await ReplaceStoredTotpDeviceAsync(sp, _spaceA, subject, ProtectWithUnpartitionedPurposeChain(sp));

        _ = await Should.ThrowAsync<CryptographicException>(() => ReadAsync(sp, _spaceA, subject));
    }

    private async Task<ServiceProvider> CreateHostAsync(bool partitionDataProtection)
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            configureServices: services =>
            {
                _ = services.AddDataProtection().UseEphemeralDataProtectionProvider();
                _ = services.Configure<SpacesOptions>(o => o.PartitionDataProtection = partitionDataProtection);
                _ = services.AddSpacesInternal();
            });
        _spaceA = await CreateSpaceAsync(_serviceProvider, "tenant-a");
        _spaceB = await CreateSpaceAsync(_serviceProvider, "tenant-b");
        return _serviceProvider;
    }

    private async Task<SpaceId> CreateSpaceAsync(ServiceProvider sp, string path)
    {
        var created = await sp.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = path,
                MatchPatterns = [new SpaceMatchPattern { Path = $"/{path}" }]
            }, _ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");
        return created.Id;
    }

    private async Task<UserSubjectId> CreateUserAsync(ServiceProvider sp, SpaceId space)
    {
        using var _ = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(space);
        var address = new ExternalAuthenticatorAddress(
            ExternalAuthenticatorName.Create("test"), OpaqueSubjectId.Create(Guid.NewGuid().ToString()));
        var result = await sp.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(address, _ct);
        return result.ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;
    }

    private async Task<UserSubjectId> EnrolTotpAsync(ServiceProvider sp, SpaceId space)
    {
        var subject = await CreateUserAsync(sp, space);
        sp.GetRequiredService<FakeTimeProvider>().SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)EnrolmentTimeSeconds));
        using var _ = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(space);

        var added = await sp.GetRequiredService<IUserAuthenticatorsSelfService>()
            .TryAddTotpDeviceAsync(subject, TotpDeviceName.Default, Key, PlainTextTotp.Create(EnrolmentTotp), _ct);

        added.ShouldBeTrue();
        return subject;
    }

    private async Task<UserAuthenticators> ReadAsync(ServiceProvider sp, SpaceId space, UserSubjectId subject)
    {
        using var _ = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(space);
        var record = await sp.GetRequiredService<UserAuthenticatorsRepository>().TryReadAsync(subject, _ct);
        return record.ShouldNotBeNull().UserAuthenticators;
    }

    private async Task<TotpDeviceDso.V1> ReadStoredTotpDeviceAsync(ServiceProvider sp, SpaceId space, UserSubjectId subject)
    {
        using var _ = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(space);
        var storage = await sp.GetRequiredService<IPartitionedStorageFactory>()
            .GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        var result = await storage.TryReadAsync(
            UserAuthenticatorsDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subject)), _ct);
        result.Found.ShouldBeTrue();
        return result.Dso.ShouldBeOfType<UserAuthenticatorsDso.V1>().TotpDevices.Single();
    }

    private async Task ReplaceStoredTotpDeviceAsync(
        ServiceProvider sp, SpaceId space, UserSubjectId subject, TotpDeviceDso.V1 device)
    {
        using var _ = sp.GetRequiredService<ISpaceContextAccessor>().SetSpace(space);
        var storage = await sp.GetRequiredService<IPartitionedStorageFactory>()
            .GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        var key = DataStorageKey.Create(UserSubjectIdDskV1.Create(subject));
        var existing = await storage.TryReadAsync(UserAuthenticatorsDso.EntityType, key, _ct);
        existing.Found.ShouldBeTrue();
        var dso = existing.Dso.ShouldBeOfType<UserAuthenticatorsDso.V1>();
        var entity = (await sp.GetRequiredService<UserAuthenticatorsRepository>().TryReadAsync(subject, _ct))
            .ShouldNotBeNull().UserAuthenticators;

        var result = await storage.ExecuteBatchAsync(
            [UpdateOperation.For(entity.Id.Uuid, dso with { TotpDevices = [device] }, existing.Version.Value, [key], [], Expiration.NoExpiration)],
            [],
            _ct);

        result.Success.ShouldBeTrue();
    }

    private static TotpDeviceDso.V1 ProtectWithUnpartitionedPurposeChain(ServiceProvider sp)
    {
        var protectedKey = sp.GetRequiredService<IDataProtectionProvider>().GetUnpartitioned()
            .CreateProtector(nameof(UserAuthenticators))
            .CreateProtector(nameof(TotpDevice))
            .CreateProtector(nameof(TotpDevice.Key))
            .Protect(Key.Bytes.ToArray());
        return new TotpDeviceDso.V1(TotpDeviceName.Default.Value, Convert.ToBase64String(protectedKey), 0);
    }
}
