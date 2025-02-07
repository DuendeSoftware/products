// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.Bff.Blazor.Client.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class BffClientAuthenticationStateProviderTests
{
    [Fact]
    public async Task when_UserService_gives_anonymous_user_GetAuthState_returns_anonymous()
    {
        var userService = Substitute.For<IGetUserService>();
        userService.GetUserAsync().Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        var sut = new BffClientAuthenticationStateProvider(
            userService,
            new FakeTimeProvider(),
            TestMocks.MockOptions(),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var authState = await sut.GetAuthenticationStateAsync();
        authState.User.Identity?.IsAuthenticated.ShouldBeFalse();
    }
    
    [Fact]
    public async Task when_UserService_returns_persisted_user_GetAuthState_returns_that_user()
    {
        var expectedName = "test-user";
        var userService = Substitute.For<IGetUserService>();
        userService.GetUserAsync().Returns(new ClaimsPrincipal(new ClaimsIdentity(
            new []{ new Claim("name", expectedName) },
            "pwd", "name", "role")));
        var sut = new BffClientAuthenticationStateProvider(
            userService,
            new FakeTimeProvider(),
            TestMocks.MockOptions(),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var authState = await sut.GetAuthenticationStateAsync();
        authState.User.Identity?.IsAuthenticated.ShouldBeTrue();
        authState.User.Identity?.Name.ShouldBe(expectedName);
        await userService.Received(1).GetUserAsync();
    }

    [Fact]
    public async Task after_configured_delay_UserService_is_called_again_and_state_notification_is_called()
    {
        var expectedName = "test-user";
        var userService = Substitute.For<IGetUserService>();
        var time = new FakeTimeProvider();
        userService.GetUserAsync().Returns(new ClaimsPrincipal(new ClaimsIdentity(
            new []{ new Claim("name", expectedName) },
            "pwd", "name", "role")));
        var sut = new BffClientAuthenticationStateProvider(
            userService,
            time,
            TestMocks.MockOptions(new BffBlazorOptions
            {
                WebAssemblyStateProviderPollingDelay = 2000,
                WebAssemblyStateProviderPollingInterval = 10000
                
            }),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var authState = await sut.GetAuthenticationStateAsync();

        // Initially, we have called the user service once to initialize
        await userService.Received(1).GetUserAsync();

        // Advance time within the polling delay, and note that we still haven't made additional calls
        time.Advance(TimeSpan.FromSeconds(1)); // t = 1
        await userService.Received(1).GetUserAsync();
        
        // Advance time past the polling delay, and note that we make an additional call
        time.Advance(TimeSpan.FromSeconds(2)); // t = 3
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(1).GetUserAsync(false);
        
        // Advance time within the polling interval, but more than the polling delay
        // We don't expect additional calls yet
        time.Advance(TimeSpan.FromSeconds(3)); // t = 6
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(1).GetUserAsync(false);
        
        // Advance time past the polling interval, and note that we make an additional call
        time.Advance(TimeSpan.FromSeconds(10)); // t = 16
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(2).GetUserAsync(false);
    }

    [Fact]
    public async Task timer_stops_when_user_logs_out()
    {
        var expectedName = "test-user";
        var userService = Substitute.For<IGetUserService>();
        var time = new FakeTimeProvider();

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        anonymousUser.Identity?.IsAuthenticated.ShouldBeFalse();

        var cachedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "cache")
        ], "pwd", "name", "role"));
        
        var fetchedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "fetch")
        ], "pwd", "name", "role"));

        userService.GetUserAsync(true).Returns(cachedUser);
        userService.GetUserAsync(false).Returns(fetchedUser, anonymousUser);
        var sut = new BffClientAuthenticationStateProvider(
            userService,
            time,
            TestMocks.MockOptions(new BffBlazorOptions
            {
                ServerStateProviderPollingDelay = 2000,
                ServerStateProviderPollingInterval = 10000
                
            }),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var authState = await sut.GetAuthenticationStateAsync();
        time.Advance(TimeSpan.FromSeconds(5));
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(1).GetUserAsync(false);

        time.Advance(TimeSpan.FromSeconds(10));
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(2).GetUserAsync(false);
        
        
        time.Advance(TimeSpan.FromSeconds(50));
        await userService.Received(1).GetUserAsync(true);
        await userService.Received(2).GetUserAsync(false);

    }
}