// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// this shim class is needed since ITicketStore is not configured in DI, rather it's a property 
/// of the cookie options and coordinated with PostConfigureApplicationCookie. #lame
/// https://github.com/aspnet/AspNetCore/issues/6946 
/// </summary>
[Obsolete(Constants.ObsoleteMessages.ImplementationWillBeMadeInternal)]
public class TicketStoreShim(IHttpContextAccessor httpContextAccessor) : ITicketStore
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly BffOptions _options = httpContextAccessor.HttpContext!.RequestServices.GetRequiredService<IOptions<BffOptions>>().Value;

    private IServerTicketStore Inner => httpContextAccessor.HttpContext!.RequestServices.GetRequiredService<IServerTicketStore>();

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        return Inner.RemoveAsync(key);
    }

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        return Inner.RenewAsync(key, ticket);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        var ticket = await Inner.RetrieveAsync(key);

        return ticket;
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        return Inner.StoreAsync(ticket);
    }
}
