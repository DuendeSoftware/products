// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Internal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Duende.UserManagement;
#pragma warning restore IDE0130

public sealed class UsersServiceProviderFactory
{
    public static async Task<ServiceProvider> CreateAsync() => await CreateAsync(false);

    public static async Task<ServiceProvider> CreateAsync(bool addDataProtection) =>
        await CreateUsersBuilderAsync(null, addDataProtection);

    public static async Task<ServiceProvider> CreateAsync(Action<IUserManagementBuilder> configureBuilder) =>
        await CreateUsersBuilderAsync(null, false, services => configureBuilder?.Invoke(new IUserManagementBuilder.Builder(services)));

    public static async Task<ServiceProvider> CreateWithOptionsAsync(Action<UserAuthenticationOptions> configureOptions) =>
        await CreateWithOptionsAsync(configureOptions, false);

    public static async Task<ServiceProvider> CreateWithOptionsAsync(Action<UserAuthenticationOptions> configureOptions, bool addDataProtection)
    {
        var sp = await CreateUsersBuilderAsync(configureOptions, addDataProtection);
        return sp;
    }

    public static async Task<ServiceProvider> CreateUsersBuilderAsync(
        Action<UserAuthenticationOptions>? configureOptions, bool addDataProtection, Action<IServiceCollection>? configureServices = null, Guid? dbId = null)
    {
        var services = CreateUsersBuilder(configureOptions, addDataProtection, dbId: dbId);
        configureServices?.Invoke(services);
        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IPooledStore>().MigrateAsync(CancellationToken.None);
        return sp;
    }

    public static IServiceCollection CreateUsersBuilder(
        Action<UserAuthenticationOptions>? configureOptions, bool addDataProtection, Action<IUserManagementBuilder>? configureBuilder = null, Guid? dbId = null)
    {
        var services = new ServiceCollection();

        dbId ??= Guid.NewGuid();
        _ = services
            .AddLogging()
            .AddSingleton(new FakeTimeProvider())
            .AddSingleton<TimeProvider>(provider => provider.GetRequiredService<FakeTimeProvider>())
            .AddSingleton(new FakeOtpSender())
            .AddSingleton<IOtpSender>(provider => provider.GetRequiredService<FakeOtpSender>());

        _ = services.AddUserManagementInternal(users =>
        {
            _ = users.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
            configureBuilder?.Invoke(users);

            // modules registered unconditionally by AddUserManagementInternal
        });

        _ = services.Configure<UserAuthenticationOptions>(options =>
        {
            options.Passkeys.ServerDomain = "example.com";
            options.Passkeys.AllowedOrigins = ["https://example.com"];
        });

        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        if (addDataProtection)
        {
            _ = services.AddDataProtection();
        }
        else
        {
            _ = services.AddSingleton<IDataProtectionProvider, NoopDataProtectionProvider>();
        }

        return services;
    }
}
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class NoopDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose) => new NoopDataProtector();

    private sealed class NoopDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => new NoopDataProtector();

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}

public static class ServicesCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection DisableDataProtection()
        {
            _ = services.AddSingleton<IDataProtectionProvider, NoopDataProtectionProvider>();

            return services;
        }
    }
}
