// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.OutsideIn.Harness.Internal;
using Duende.Spaces;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// The logical IdentityServer a scenario runs against (design doc section 09, D35): the system under test itself
/// without Spaces, or one space with Spaces on. Everything on it is scoped to that issuer.
/// </summary>
public interface IIssuer
{
    /// <summary>Identifies the issuer within the environment, for example <c>sut</c>, or <c>space-1</c> with Spaces on.</summary>
    string Name { get; }

    /// <summary>The issuer's public URL, for example <c>https://sut</c>. Appears as <c>{sut}</c> in snapshots.</summary>
    Uri Url { get; }

    /// <summary>The issuer's discovery document, <c>{Url}/.well-known/openid-configuration</c>.</summary>
    Uri DiscoveryUrl { get; }

    /// <summary>The issuer's token endpoint, <c>{Url}/connect/token</c>, for scenarios that redeem a grant themselves.</summary>
    Uri TokenEndpoint { get; }

    /// <summary>The issuer's introspection endpoint, <c>{Url}/connect/introspect</c>.</summary>
    Uri IntrospectionEndpoint { get; }

    /// <summary>Seeds and changes this issuer's data.</summary>
    ITestStorage Storage { get; }

    /// <summary>Creates users that belong to this issuer.</summary>
    ITestUsers TestUsers { get; }

    /// <summary>Creates clients that belong to this issuer.</summary>
    ITestClients TestClients { get; }

    /// <summary>Creates API resources that belong to this issuer.</summary>
    ITestResources TestResources { get; }
}

/// <summary>Creates test users scoped to one issuer (design doc section 08, D14).</summary>
public interface ITestUsers
{
    /// <summary>
    /// Creates a complete user with a generated subject ID, a username unique to this issuer and a default password. All
    /// three are registered for scrubbing, so they appear as <c>{user:name}</c>, <c>{username:name}</c> and
    /// <c>{password:name}</c> in snapshots, or with <c>@issuer</c> added when they show up in another issuer's run.
    /// </summary>
    TestUser Create(string username);
}

/// <summary>Creates test API resources scoped to one issuer (design doc section 08, D14).</summary>
public interface ITestResources
{
    /// <summary>
    /// A complete API resource that exposes <paramref name="scopes" /> and can authenticate at the introspection
    /// endpoint with the returned plaintext secret. Both the resource's name and its secret are unique to this issuer
    /// and registered for scrubbing, as <c>{resource:name}</c> and <c>{secret:name}</c>.
    /// </summary>
    (ApiResource Resource, string Secret) ApiResourceWithSecret(string name, params string[] scopes);
}

internal sealed class Issuer : IIssuer, ITestUsers, ITestResources
{
    private readonly SnapshotScrubber _scrubber;

    private readonly TestStorage _storage;

    public Issuer(string name, IdentityServerTopology server, SnapshotScrubber scrubber, Uri? url = null, SpaceMatchPattern? spaceMatch = null)
    {
        SpaceMatch = spaceMatch;
        Name = name;
        Url = url ?? server.BaseUrl;
        _scrubber = scrubber;
        // Every issuer is the system under test to the outside world, so its values carry that name in snapshots, like its URL.
        _storage = new TestStorage(TestEnvironment.SystemUnderTestName, owner: name, Url, server, scrubber);
    }

    public string Name { get; }

    public SpaceMatchPattern? SpaceMatch { get; }

    public Uri Url { get; }

    public Uri DiscoveryUrl => new($"{Url.GetLeftPart(UriPartial.Path).TrimEnd('/')}/.well-known/openid-configuration");

    public Uri TokenEndpoint => new($"{Url.GetLeftPart(UriPartial.Path).TrimEnd('/')}/connect/token");

    public Uri IntrospectionEndpoint => new($"{Url.GetLeftPart(UriPartial.Path).TrimEnd('/')}/connect/introspect");

    public ITestStorage Storage => _storage;

    /// <summary>The space this issuer is, once the environment has created it. <c>null</c> without Spaces.</summary>
    public SpaceId? SpaceId
    {
        get => _storage.SpaceId;
        set => _storage.SpaceId = value;
    }

    public ITestUsers TestUsers => this;

    public ITestClients TestClients { get; } = new TestClients();

    public ITestResources TestResources => this;

    public TestUser Create(string username)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        // Unique per issuer (and per test), so every value that identifies the user shows which issuer it belongs to.
        var user = new TestUser(
            $"{username}-{Name}-{Guid.NewGuid().ToString("N")[..8]}",
            subjectId: Guid.NewGuid().ToString("N"),
            password: Guid.NewGuid().ToString("N"),
            issuer: Name);
        _scrubber.Register(user.Username, $"username:{username}", owner: Name);
        _scrubber.Register(user.SubjectId, $"user:{username}", owner: Name);
        _scrubber.Register(user.Password, $"password:{username}", owner: Name);
        return user;
    }

    public (ApiResource Resource, string Secret) ApiResourceWithSecret(string name, params string[] scopes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Unique per issuer (and per test), like TestUsers.Create, so names and secrets never collide across issuers.
        var resourceName = $"{name}-{Name}-{Guid.NewGuid().ToString("N")[..8]}";
        var secret = Guid.NewGuid().ToString("N");
        _scrubber.Register(resourceName, $"resource:{name}", owner: Name);
        _scrubber.Register(secret, $"secret:{name}", owner: Name);

        var resource = new ApiResource(resourceName) { ApiSecrets = { TestSecrets.Create(secret) } };
        foreach (var scope in scopes)
        {
            resource.Scopes.Add(scope);
        }

        return (resource, secret);
    }
}
