// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.TestIsolation;

public sealed record TestRegistration(
    RequestDelegate Pipeline,
    IServiceProvider ServiceProvider) : IDisposable
{
    public void Dispose()
    {
        if (ServiceProvider is IDisposable d)
        {
            d.Dispose();
        }
    }
}
