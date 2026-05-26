// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal.Modules;

internal interface IDuendeModule
{
    static abstract void Register(IServiceCollection services);
}

/// <summary>
/// A module that also maps HTTP endpoints.
/// </summary>
internal interface IHttpModule : IDuendeModule
{
    /// <summary>
    /// Maps this module's HTTP endpoints.
    /// </summary>
    /// <param name="app">The web application to map endpoints to.</param>
    void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder;
}

internal interface IDuendePlatformFeature
{
    string Name { get; }
}
