// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.IdentityServer.Internal;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class RegisteredImplementationsDiagnosticEntry : IDiagnosticEntry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IEnumerable<Type>> _typesToInspect;

    public RegisteredImplementationsDiagnosticEntry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        _typesToInspect = assemblies.SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsInterface && type.IsPublic && !type.IsGenericTypeDefinition && type.Namespace != null && type.Namespace.StartsWith("Duende.IdentityServer"))
            .GroupBy(type => type.Namespace, type => type)
            .ToDictionary(group => group.Key, group => group.OrderBy(type => type.Name).AsEnumerable());
    }

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        using var scope = _serviceProvider.CreateScope();

        writer.WriteStartObject("RegisteredImplementations");

        foreach (var namespaceGroup in _typesToInspect)
        {
            writer.WriteStartArray(namespaceGroup.Key);

            foreach (var type in namespaceGroup.Value)
            {
                WriteImplementationDetails(type, type.Name, writer, scope);
            }

            writer.WriteEndArray();
        }

        //INFO: Types which are registered as open generics are intentionally not included in the above loop as
        //they cannot be resolved from the DI container as an open generic type. Rather than attempting to dynamically
        //create a closed type, which could be error-prone, we'll explicitly list the open generic types we know about.
        writer.WriteStartArray("OpenGenericTypes");
        WriteImplementationDetails<ICache<string>>(nameof(ICache<string>), writer, scope);
        WriteImplementationDetails<IConcurrencyLock<string>>(nameof(IConcurrencyLock<string>), writer, scope);
        WriteImplementationDetails<IMessageStore<string>>(nameof(IMessageStore<string>), writer, scope);
        writer.WriteEndArray();

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private void WriteImplementationDetails<T>(string serviceName, Utf8JsonWriter writer, IServiceScope scope) where T : class => WriteImplementationDetails(typeof(T), serviceName, writer, scope);

    private void WriteImplementationDetails(Type targetType, string serviceName, Utf8JsonWriter writer, IServiceScope scope)
    {
        writer.WriteStartObject();
        writer.WriteStartArray(serviceName);

        var services = scope.ServiceProvider.GetServices(targetType).Where(service => service != null);
        if (services.Any())
        {
            foreach (var service in services)
            {
                var type = service.GetType();
                writer.WriteStartObject();
                writer.WriteString("TypeName", type.FullName);
                writer.WriteString("Assembly", type.Assembly.GetName().Name);
                writer.WriteString("AssemblyVersion", type.Assembly.GetName().Version?.ToString());
                writer.WriteEndObject();
            }
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteString("TypeName", "Not Registered");
            writer.WriteString("Assembly", "Not Registered");
            writer.WriteString("AssemblyVersion", "Not Registered");
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
