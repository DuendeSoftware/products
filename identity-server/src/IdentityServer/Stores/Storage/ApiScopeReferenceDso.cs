// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// Represents a reference to an ApiScope entity, storing both Id and Name
/// for stable linking and human-readable display.
/// </summary>
internal static class ApiScopeReferenceDso
{
    internal sealed record V1(Guid Id, string Name);
}
