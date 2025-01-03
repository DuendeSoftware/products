// // Copyright (c) Duende Software. All rights reserved.
// // See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.Bff.Yarp;

/// <summary>
/// YARP related DI extension methods
/// </summary>
public static class BffBuilderExtensions
{
    /// <summary>
    /// Adds the services required for the YARP HTTP forwarder
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static BffBuilder AddRemoteApis(this BffBuilder builder)
    {
        builder.Services.AddHttpForwarder();

        builder.Services.TryAddSingleton<IHttpMessageInvokerFactory, DefaultHttpMessageInvokerFactory>();
        builder.Services.TryAddSingleton<IHttpTransformerFactory, DefaultHttpTransformerFactory>();

        return builder;
    }

    /// <summary>
    /// Adds a custom HttpMessageInvokerFactory to DI
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static BffBuilder AddHttpMessageInvokerFactory<T>(this BffBuilder builder)
        where T : class, IHttpMessageInvokerFactory
    {
        builder.Services.AddTransient<IHttpMessageInvokerFactory, T>();
        
        return builder;
    }
        
    /// <summary>
    /// Adds a custom HttpTransformerFactory to DI
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static BffBuilder AddHttpTransformerFactory<T>(this BffBuilder builder)
        where T : class, IHttpTransformerFactory
    {
        builder.Services.AddTransient<IHttpTransformerFactory, T>();
        
        return builder;
    }
}