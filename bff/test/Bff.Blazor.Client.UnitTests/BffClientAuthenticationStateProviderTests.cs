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
    public async Task when_no_user_in_persistent_state_GetAuthState_returns_anonymous_and_does_not_poll()
    {
        var userService = Substitute.For<GetUserService>();
        var persistentUserService = Substitute.For<PersistentUserService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        persistentUserService.GetPersistedUser(out Arg.Any<ClaimsPrincipal?>())
            .Returns(x =>
            {
                x[0] = anonymous;
                return true;
            });
        var time = new FakeTimeProvider();

        var sut = new BffClientAuthenticationStateProvider(
            userService,
            persistentUserService,
            time,
            TestMocks.MockOptions(),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var authState = await sut.GetAuthenticationStateAsync();
        authState.User.Identity?.IsAuthenticated.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(100));
        await userService.DidNotReceive().GetUserAsync();
    }
    
    [Fact]
    public async Task when_user_in_persistent_state_GetAuthState_returns_that_user_and_then_polls_user_endpoint()
    {
        var time = new FakeTimeProvider();
        var expectedName = "test-user";
        var getUserService = Substitute.For<GetUserService>();
        var persistentUserService = Substitute.For<PersistentUserService>();
        var persistedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "cache")
        ], "pwd", "name", "role"));
        var fetchedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "fetch")
        ], "pwd", "name", "role"));        
        persistentUserService.GetPersistedUser(out Arg.Any<ClaimsPrincipal?>())
            .Returns(x =>
            {
                x[0] = persistedUser;
                return true;
            });
        getUserService.GetUserAsync().Returns(fetchedUser);

        var sut = new BffClientAuthenticationStateProvider(
            getUserService,
            persistentUserService,
            time,
            TestMocks.MockOptions(new BffBlazorOptions
            {
                WebAssemblyStateProviderPollingDelay = 2000,
                WebAssemblyStateProviderPollingInterval = 10000
            }),
    Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());
        
        var authState = await sut.GetAuthenticationStateAsync();
        authState.User.Identity?.IsAuthenticated.ShouldBeTrue();
        authState.User.Identity?.Name.ShouldBe(expectedName);
        // Initially we get the persisted user and haven't yet polled
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await getUserService.DidNotReceive().GetUserAsync();
        
        // Advance time within the polling delay, and note that we still haven't made additional calls
        time.Advance(TimeSpan.FromSeconds(1)); // t = 1
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await getUserService.DidNotReceive().GetUserAsync();
        
        // Advance time past the polling delay, and note that we make an additional call to fetch the user
        time.Advance(TimeSpan.FromSeconds(2)); // t = 3
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await getUserService.Received(1).GetUserAsync();
        
        // Advance time within the polling interval, but more than the polling delay
        // We don't expect additional calls yet
        time.Advance(TimeSpan.FromSeconds(3)); // t = 6
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await getUserService.Received(1).GetUserAsync();
        
        // Advance time past the polling interval, and note that we make an additional call
        time.Advance(TimeSpan.FromSeconds(10)); // t = 16
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await getUserService.Received(2).GetUserAsync();
    }

    [Fact]
    public async Task timer_stops_when_user_logs_out()
    {
        var expectedName = "test-user";
        var userService = Substitute.For<GetUserService>();
        var persistentUserService = Substitute.For<PersistentUserService>();
        var time = new FakeTimeProvider();

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var persistedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "cache")
        ], "pwd", "name", "role"));
        var fetchedUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("name", expectedName),
            new Claim("source", "fetch")
        ], "pwd", "name", "role"));

        persistentUserService.GetPersistedUser(out Arg.Any<ClaimsPrincipal?>())
            .Returns(x =>
            {
                x[0] = persistedUser;
                return true;
            });
        // Simulate that the user got logged out by first returning a mocked logged in user,
        // and then returning an anonymous user
        userService.GetUserAsync().Returns(fetchedUser, anonymousUser);
        
        var sut = new BffClientAuthenticationStateProvider(
            userService,
            persistentUserService,
            time,
            TestMocks.MockOptions(),
            Substitute.For<ILogger<BffClientAuthenticationStateProvider>>());

        var _ = await sut.GetAuthenticationStateAsync();
        time.Advance(TimeSpan.FromSeconds(5));
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await userService.Received(1).GetUserAsync();

        time.Advance(TimeSpan.FromSeconds(10));
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await userService.Received(2).GetUserAsync();
        
        time.Advance(TimeSpan.FromSeconds(50));
        persistentUserService.Received(1).GetPersistedUser(out Arg.Any<ClaimsPrincipal?>());
        await userService.Received(2).GetUserAsync();

    }
}