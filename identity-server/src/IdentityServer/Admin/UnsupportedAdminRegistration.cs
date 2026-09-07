// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Admin;

/// <summary>
/// Helpers for registering admin service interfaces as unsupported when the active
/// configuration store does not provide an admin implementation.
/// </summary>
/// <remarks>
/// Only Duende.Storage-backed registration (<c>AddStorage()</c>) provides working admin
/// services. When configuration data is served from an in-memory or Entity Framework store,
/// the admin interfaces are registered as throwing stubs so that attempting to resolve or use
/// them fails with a clear, actionable message instead of a generic "no service registered"
/// error or silently operating against the wrong store.
/// </remarks>
public static class UnsupportedAdminRegistration
{
    /// <summary>
    /// Registers <typeparamref name="TAdmin"/> so that resolving it throws a
    /// <see cref="NotSupportedException"/> explaining that admin services require
    /// Duende.Storage-backed registration.
    /// </summary>
    /// <typeparam name="TAdmin">The admin service interface to disable.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="storeDescription">
    /// A short description of the active store, used in the exception message (for example
    /// "in-memory stores" or "Entity Framework storage").
    /// </param>
    public static void DisableAdmin<TAdmin>(this IServiceCollection services, string storeDescription)
        where TAdmin : class =>
        services.AddTransient<TAdmin>(_ => throw new NotSupportedException(
            $"{typeof(TAdmin).Name} is not supported when using {storeDescription}. " +
            "Admin services are only available when configuration is registered via AddStorage()."));
}
