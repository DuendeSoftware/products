// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer8.UnitTests;

public sealed class OtpHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithSmtpDispatcher(WebApplicationBuilder builder)
    {
        // begin-snippet: otp-use-smtp-dispatcher
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth.UseSmtpOtpDispatcher(o =>
            {
                o.Host = "smtp.example.com";
                o.FromEmail = "no-reply@example.com";
                o.FromName = "Contoso";
            })));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task UseSmtpOtpDispatcher_registers_the_SMTP_dispatcher()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithSmtpDispatcher(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        app.Services.GetRequiredService<IOtpDispatcher>().GetType().Name.ShouldBe("SmtpOtpDispatcher");
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithCustomDispatcher(WebApplicationBuilder builder)
    {
        // begin-snippet: otp-register-custom-dispatcher
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth.UseOtpDispatcher<WebhookOtpDispatcher>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task UseOtpDispatcher_registers_the_custom_dispatcher()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithCustomDispatcher(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.Services.GetRequiredService<IOtpDispatcher>().ShouldBeOfType<WebhookOtpDispatcher>();
    }
}

// begin-snippet: otp-custom-dispatcher
internal sealed class WebhookOtpDispatcher : IOtpDispatcher
{
    public bool CanDispatch(OtpAddress address) => address.Channel == OtpChannel.Sms;

    public Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct) =>
        throw new NotImplementedException("Send the code through your SMS provider here.");
}
// end-snippet
