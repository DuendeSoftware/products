// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal interface IConfigurationValidator<in T> where T : class
{
    Task<IReadOnlyList<AdminError>> ValidateAsync(T configuration, Ct ct);
}
