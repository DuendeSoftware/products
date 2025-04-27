// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Test;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for the IdentityServer builder
/// </summary>
public static class IdentityServerBuilderExtensions
{
    /// <summary>
    /// Adds test users.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="users">The users.</param>
    public static IIdentityServerBuilder AddTestUsers(this IIdentityServerBuilder builder, List<TestUser> users)
    {
        builder.Services.AddSingleton(new TestUserStore(users));
        builder.AddProfileService<TestUserProfileService>();
        builder.AddResourceOwnerValidator<TestUserResourceOwnerPasswordValidator>();

        builder.AddBackchannelAuthenticationUserValidator<TestBackchannelLoginUserValidator>();

        return builder;
    }
}
