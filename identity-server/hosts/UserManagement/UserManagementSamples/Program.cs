// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.CommandLine;
using Projects;

var programName = typeof(Program).Assembly.GetName().Name!.Replace(".", "-", StringComparison.Ordinal);

Option<Profile> profileOption = new("-p", "--profile") { DefaultValueFactory = _ => Profile.All };
var cmd = new RootCommand { profileOption };

cmd.SetAction(async cmdLine =>
{
    var builder = DistributedApplication.CreateBuilder(args);

    var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
        .WithContainerName($"{programName}-mailpit")
        .WithEndpoint(1026, 1025, "smtp", name: "mailpit-smtp", isProxied: false)
        .WithEndpoint(8026, 8025, "http", name: "mailpit-http", isProxied: false);

    var smtpEndpoint = mailpit.GetEndpoint("mailpit-smtp");

    var db = builder
        .AddPostgres($"postgres")
        .WithContainerName($"{programName}-postgres")
        .WithDataVolume($"{programName}-postgres-data")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithPgAdmin(configureContainer => configureContainer
            .WithContainerName($"{programName}-pgadmin")
            .WithLifetime(ContainerLifetime.Persistent))
        .AddDatabase("postgres-db");

    var profile = cmdLine.GetValue(profileOption);

    if (profile is Profile.All or Profile.AuthenticationFlowWithWebComponent)
    {
        _ = builder.AddProject<AuthenticationFlowWithWebComponent>("authentication-component-flow")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.ExternalAuthentication)
    {
        _ = builder.AddProject<ExternalAuthentication>("external-authentication")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.MfaStepUp)
    {
        _ = builder.AddProject<MfaStepUp>("mfa-step-up")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.OtpAuthentication)
    {
        _ = builder.AddProject<OtpAuthentication>("otp-authentication")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.OtpIdentityServer)
    {
        var otpIdentityServer = builder.AddProject<OtpIdentityServer>("otp-identity-server", launchProfileName: "https")
            .WithReference(db, "pgsql")
            .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
            .WaitFor(db);

        _ = builder.AddProject<SimpleWebApp>("simple-web-app", launchProfileName: "https")
            .WithEndpoint("https", endpoint => endpoint.IsProxied = false)
            .WaitFor(otpIdentityServer);
    }

    if (profile is Profile.All or Profile.PasswordValidation)
    {
        _ = builder.AddProject<PasswordValidation>("password-validation")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.PasskeyAuthentication)
    {
        _ = builder.AddProject<PasskeyAuthentication>("passkey-authentication")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.PasskeyAuthenticationAs2FA)
    {
        _ = builder.AddProject<PasskeyAuthenticationAs2FA>("passkey-authentication-as-2fa")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.PasskeyAuthenticationSdk)
    {
        _ = builder.AddProject<PasskeyAuthentication_Sdk>("passkey-authentication-sdk")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.TotpAuthentication)
    {
        _ = builder.AddProject<TotpAuthentication>("totp-authentication")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.UserSearch)
    {
        _ = builder.AddProject<UserSearch>("user-search")
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    if (profile is Profile.All or Profile.UserManagement)
    {
        _ = builder.AddProject<PlatformUserManagementApp>("platform-user-management-app")
            .WithSmtp(smtpEndpoint)
            .WithReference(db, "pgsql")
            .WaitFor(db);

        _ = builder.AddProject<AspNetIdentityApp>("aspnet-identity-app")
            .WithSmtp(smtpEndpoint)
            .WithReference(db, "pgsql")
            .WaitFor(db);
    }

    await builder.Build().RunAsync();
});

return await cmd.Parse(args).InvokeAsync();

internal enum Profile
{
    All,
    AuthenticationFlowWithWebComponent,
    ExternalAuthentication,
    MfaStepUp,
    OtpAuthentication,
    OtpIdentityServer,
    PasskeyAuthentication,
    PasskeyAuthenticationAs2FA,
    PasskeyAuthenticationSdk,
    PasswordValidation,
    TotpAuthentication,
    UserManagement,
    UserSearch
}

internal static class AspireExtensions
{
    /// <summary>
    /// Injects SMTP configuration (Smtp:Host, Smtp:Port, Smtp:EnableSsl) from a mailpit endpoint.
    /// </summary>
    internal static IResourceBuilder<T> WithSmtp<T>(this IResourceBuilder<T> project, EndpointReference smtpEndpoint)
        where T : IResourceWithEnvironment =>
        project
            .WithEnvironment("Smtp__Host", smtpEndpoint.Property(EndpointProperty.Host))
            .WithEnvironment("Smtp__Port", smtpEndpoint.Property(EndpointProperty.Port))
            .WithEnvironment("Smtp__EnableSsl", "false");
}
