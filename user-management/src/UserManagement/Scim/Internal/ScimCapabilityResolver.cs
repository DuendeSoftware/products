// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Resolves SCIM capabilities by merging explicit option overrides with
/// auto-detection from DI service registrations.
/// </summary>
internal sealed class ScimCapabilityResolver
{
    private readonly IServiceProvider _services;
    private readonly ScimOptions _options;

    public ScimCapabilityResolver(IServiceProvider services, IOptions<ScimOptions> options)
    {
        _services = services;
        _options = options.Value;
    }

    /// <summary>Whether SCIM User resource endpoints are available.</summary>
    public bool UsersEnabled =>
        _options.EnableUsers ?? _services.GetService<IUserProfileAdmin>() is not null;

    /// <summary>Whether SCIM Group resource endpoints are available.</summary>
    public bool GroupsEnabled =>
        _options.EnableGroups ?? _services.GetService<IGroupAdmin>() is not null;

    /// <summary>Whether the SCIM changePassword capability is supported.</summary>
    public bool ChangePasswordSupported =>
        _options.ChangePassword ?? _services.GetService<IPasswordAuth>() is not null;

    /// <summary>Maximum number of resources returned in a single list response.</summary>
    public int MaxResults => _options.MaxResults;

    /// <summary>Maximum number of operations allowed in a single bulk request.</summary>
    public int MaxBulkOperations => _options.MaxBulkOperations;

    /// <summary>Maximum payload size in bytes for a single bulk request.</summary>
    public int MaxBulkPayloadSize => _options.MaxBulkPayloadSize;
}
