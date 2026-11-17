// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// What <see cref="TestEnvironmentBuilder.SetupIdentityServer" /> and
/// <see cref="TestEnvironmentBuilder.AddRemoteIdentityServer" /> pass to the test to configure a server (design doc
/// section 05): features and pages (section 07). Storage, caching, users and Spaces come from the
/// <see cref="Permutation" />, and the node count is fixed at 2 (D36), so none of those are set here.
/// </summary>
public sealed class IdentityServerTopologyBuilder
{
    private readonly UserStore _users;
    private string? _loginPageName;

    internal IdentityServerTopologyBuilder(UserStore users) => _users = users;

    internal Action<WebApplication>? LoginPage { get; private set; }

    /// <summary>Users the server knows from the start, because one of its pages needs them.</summary>
    internal List<TestUser> InitialUsers { get; } = [];

    /// <summary>
    /// A login form the TestBrowser fills in, checked against IdentityServer's test users (D20), or against User
    /// Management (D9) when the permutation's <see cref="UserStore" /> is <see cref="UserStore.UserManagement" />.
    /// </summary>
    public IdentityServerTopologyBuilder WithPasswordLogin() =>
        _users == UserStore.UserManagement
            ? SetLoginPage(nameof(WithPasswordLogin), LoginPages.MapUserManagementPasswordLogin)
            : SetLoginPage(nameof(WithPasswordLogin), LoginPages.MapPasswordLogin);

    /// <summary>A login page that sends the user to the issuer's remote IdP and handles the callback.</summary>
    public IdentityServerTopologyBuilder WithExternalLogin() =>
        SetLoginPage(nameof(WithExternalLogin), LoginPages.MapExternalLogin);

    /// <summary>A login page that signs <paramref name="user" /> in straight away. The server knows the user from the start.</summary>
    public IdentityServerTopologyBuilder WithAutoSignIn(TestUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        InitialUsers.Add(user);
        return SetLoginPage(nameof(WithAutoSignIn), app => LoginPages.MapAutoSignIn(app, user));
    }

    private IdentityServerTopologyBuilder SetLoginPage(string name, Action<WebApplication> map)
    {
        if (_loginPageName is not null)
        {
            throw new InvalidOperationException($"A server has one login page. {name} can't be added after {_loginPageName}.");
        }

        _loginPageName = name;
        LoginPage = map;
        return this;
    }
}
