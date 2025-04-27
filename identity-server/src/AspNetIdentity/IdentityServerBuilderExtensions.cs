// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to add ASP.NET Identity support to IdentityServer.
/// </summary>
public static class IdentityServerBuilderExtensions
{
    /// <summary>
    /// Configures IdentityServer to use the ASP.NET Identity implementations 
    /// of IUserClaimsPrincipalFactory, IResourceOwnerPasswordValidator, and IProfileService.
    /// Also configures some of ASP.NET Identity's options for use with IdentityServer (such as claim types to use
    /// and authentication cookie settings).
    /// </summary>
    /// <typeparam name="TUser">The type of the user.</typeparam>
    /// <param name="builder">The builder.</param>
    public static IIdentityServerBuilder AddAspNetIdentity<TUser>(this IIdentityServerBuilder builder)
        where TUser : class
    {
        builder.Services.AddTransientDecorator<IUserClaimsPrincipalFactory<TUser>, UserClaimsFactory<TUser>>();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = JwtClaimTypes.Subject;
            options.ClaimsIdentity.UserNameClaimType = JwtClaimTypes.Name;
            options.ClaimsIdentity.RoleClaimType = JwtClaimTypes.Role;
            options.ClaimsIdentity.EmailClaimType = JwtClaimTypes.Email;
        });

        builder.Services.Configure<SecurityStampValidatorOptions>(opts =>
        {
            opts.OnRefreshingPrincipal = SecurityStampValidatorCallback.UpdatePrincipal;
        });

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.IsEssential = true;
            // we need to disable to allow iframe for authorize requests
            options.Cookie.SameSite = AspNetCore.Http.SameSiteMode.None;
        });

        builder.Services.ConfigureExternalCookie(options =>
        {
            options.Cookie.IsEssential = true;
            // https://github.com/IdentityServer/IdentityServer4/issues/2595
            options.Cookie.SameSite = AspNetCore.Http.SameSiteMode.None;
        });

        builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
        {
            options.Cookie.IsEssential = true;
        });

        builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
        {
            options.Cookie.IsEssential = true;
        });

        builder.Services.AddAuthentication(options =>
        {
            if (options.DefaultAuthenticateScheme == null &&
                options.DefaultScheme == IdentityServerConstants.DefaultCookieAuthenticationScheme)
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
            }
        });

        builder.AddResourceOwnerValidator<ResourceOwnerPasswordValidator<TUser>>();
        builder.AddProfileService<ProfileService<TUser>>();

        builder.Services.AddSingleton<IPostConfigureOptions<IdentityServerOptions>, UseAspNetIdentityCookieScheme>();

        return builder;
    }

    internal static void AddTransientDecorator<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddDecorator<TService>();
        services.AddTransient<TService, TImplementation>();
    }

    internal static void AddDecorator<TService>(this IServiceCollection services)
    {
        var registration = services.LastOrDefault(x => x.ServiceType == typeof(TService));
        if (registration == null)
        {
            throw new InvalidOperationException("Service type: " + typeof(TService).Name + " not registered.");
        }
        if (services.Any(x => x.ServiceType == typeof(Decorator<TService>)))
        {
            throw new InvalidOperationException("Decorator already registered for type: " + typeof(TService).Name + ".");
        }

        services.Remove(registration);

        if (registration.ImplementationInstance != null)
        {
            var type = registration.ImplementationInstance.GetType();
            var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), type);
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(type, registration.ImplementationInstance));
        }
        else if (registration.ImplementationFactory != null)
        {
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), provider =>
            {
                return new DisposableDecorator<TService>((TService)registration.ImplementationFactory(provider));
            }, registration.Lifetime));
        }
        else if (registration.ImplementationType != null)
        {
            var type = registration.ImplementationType;
            var innerType = typeof(Decorator<,>).MakeGenericType(typeof(TService), registration.ImplementationType);
            services.Add(new ServiceDescriptor(typeof(Decorator<TService>), innerType, ServiceLifetime.Transient));
            services.Add(new ServiceDescriptor(type, type, registration.Lifetime));
        }
        else
        {
            throw new InvalidOperationException("Invalid registration in DI for: " + typeof(TService).Name);
        }
    }
}
