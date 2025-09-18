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
public class TicketStoreShim : ITicketStore
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly BffOptions _options;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public TicketStoreShim(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = _httpContextAccessor.HttpContext!.RequestServices.GetRequiredService<IOptions<BffOptions>>().Value;
    }

    /// <summary>
    /// The inner
    /// </summary>
    private IServerTicketStore Inner => _httpContextAccessor.HttpContext!.RequestServices.GetRequiredService<IServerTicketStore>();

    /// <inheritdoc />
    public Task RemoveAsync(string key) => Inner.RemoveAsync(key);

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket) => Inner.RenewAsync(key, ticket);

    /// <inheritdoc />
    public async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        var ticket = await Inner.RetrieveAsync(key);

        return ticket;
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket) => Inner.StoreAsync(ticket);
}
