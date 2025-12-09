// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.AccessTokenManagement;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Blazor;
using Duende.Bff.Blazor.Client;
using Duende.Bff.Blazor.Client.Internals;
using Duende.Bff.DynamicFrontends.Internal;
using Duende.Bff.Endpoints.Internal;
using Duende.Bff.EntityFramework;
using Duende.Bff.Internal;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Duende.Bff.Yarp;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class ConventionTests(ITestOutputHelper output)
{
    public static readonly Assembly BffAssembly = typeof(BffBuilder).Assembly;
    public static readonly Assembly BffBlazorAssembly = typeof(BffBlazorServerOptions).Assembly;
    public static readonly Assembly BffBlazorClientAssembly = typeof(BffBlazorClientOptions).Assembly;
    public static readonly Assembly BffEntityFrameworkAssembly = typeof(UserSessionEntity).Assembly;
    public static readonly Assembly BffYarpAssembly = typeof(BffYarpTransformBuilder).Assembly;
    public static readonly Type[] AllTypes =
        BffAssembly.GetTypes()
            .Union(BffBlazorAssembly.GetTypes())
            .Union(BffBlazorClientAssembly.GetTypes())
            .Union(BffEntityFrameworkAssembly.GetTypes())
            .Union(BffYarpAssembly.GetTypes())
        .ToArray();

    [Fact]
    public void All_strongly_typed_strings_Have_private_value()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            var stringFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(string)).ToList();
            stringFields.ShouldNotBeEmpty($"{type.Name} should have a private string field.");
            stringFields.All(f => f.IsPrivate).ShouldBeTrue($"{type.Name} should have its string value as private.");
        }
    }


    [Fact]
    public void All_strongly_typed_strings_are_readonly_struct()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            type.IsValueType.ShouldBeTrue($"{type.Name} should be a value type (struct).");
            type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute));
        }
    }

    [Fact]
    public void All_strongly_typed_strings_have_internal_create_method()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            var buildMethod = type.GetMethods(BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Create");
            buildMethod.ShouldBeNull("The IStonglyTypedString defines a Create method, but it should be implemented explicitly on the interface, not on the type. \r\n IE: " +
                "    static AccessTokenString IStonglyTypedString<AccessTokenString>.Create(string result) => new(result);");
        }
    }

    [Fact]
    public void All_strongly_typed_strings_should_have_public_constructor_that_throws()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            // Find the public constructor that takes a single string parameter
            var ctor = type.GetConstructor([]);
            ctor.ShouldNotBeNull($"{type.Name} should have a public parameterless constructor.");

            // Try to invoke the constructor with a value and expect an exception
            var ex = Should.Throw<TargetInvocationException>(() => ctor.Invoke([]));
            ex.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("Can't create null value");
        }
    }
    [Fact]
    public void All_strongly_typed_strings_should_have_only_expected_constructors()
    {
        var stringValueTypes = GetStrongTypedStringTypes();
        foreach (var type in stringValueTypes)
        {
            // Get all instance constructors (public and non-public)
            var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // There must be exactly two constructors
            ctors.Length.ShouldBe(2, $"{type.Name} should have exactly two constructors: one public parameterless and one private with a single string parameter.");

            // Find the public parameterless constructor
            var publicParameterlessCtor = ctors.FirstOrDefault(c =>
                c.IsPublic &&
                c.GetParameters().Length == 0);

            publicParameterlessCtor.ShouldNotBeNull($"{type.Name} should have a public parameterless constructor.");

            // Find the private constructor with a single string parameter
            var privateStringCtor = ctors.FirstOrDefault(c =>
                c.IsPrivate &&
                c.GetParameters().Length == 1 &&
                c.GetParameters()[0].ParameterType == typeof(string));

            privateStringCtor.ShouldNotBeNull($"{type.Name} should have a private constructor with a single string parameter.");
        }
    }

    [Fact]
    public void All_types_in_Internal_namespace_should_be_internal()
    {
        // Find all types in the 'Duende.AccessTokenManagement.Internal' namespace
        var internalTypes = AllTypes
            .Where(t => t.Namespace != null)
            .Where(t => t.Namespace!.Contains(".Internal"))
            .ToList();

        internalTypes.ShouldNotBeEmpty("No types found in the 'Duende.AccessTokenManagement.Internal' namespace.");

        foreach (var type in internalTypes)
        {
            IsInternal(type).ShouldBeTrue($"{type.Name} should be internal.");
        }
    }

    [Fact()]
    public void All_types_not_in_Internal_namespace_should_be_sealed_or_static()
    {
        Type[] exclusions = [
            typeof(SessionDbContext),
            typeof(SessionDbContext<>),
            typeof(UserSessionEntity),
            typeof(UserSession),
            typeof(UserSessionUpdate),
            typeof(AccessTokenRetrievalError)];

        // Find all types NOT in a '.Internal' namespace
        var nonInternalTypes = AllTypes
            .Where(t => t.Namespace != null && !t.Namespace.Contains(".Internal"))
            .Where(t => !IsInternal(t))
            .Where(t => t.IsClass && !t.IsAbstract) // Only consider non-abstract classes
            .Where(t => !exclusions.Contains(t))
            .ToList();

        nonInternalTypes.ShouldNotBeEmpty("No non-internal types found.");

        foreach (var type in nonInternalTypes)
        {
            // A static class is abstract and sealed
            var isStatic = type.IsAbstract && type.IsSealed;
            var isSealed = type.IsSealed;

            (isSealed || isStatic).ShouldBeTrue(
                $"{type.FullName} should be sealed or static (abstract+sealed)."
            );
        }
    }

    [Fact]
    public void All_async_methods_should_end_with_Async_and_have_cancellation_token_as_last_parameter()
    {
        var failures = new List<string>();
        Type[] exclusions = [
            typeof(BffAuthenticationSchemeProvider),
            typeof(BffOpenIdConnectEvents),
            typeof(BffAuthenticationService),
            typeof(TicketStoreShim),
            typeof(ServerSideTicketStore),
            typeof(AddServerManagementClaimsTransform),
            typeof(BffServerAuthenticationStateProvider),
            typeof(BffClientAuthenticationStateProvider),
            typeof(SessionCleanupHost),
            typeof(SessionDbContext),
            typeof(SessionDbContext<>),
            typeof(ServerSideTokenStore), // This one needs to be removed after move to ATM 4.0

        ];
        foreach (var type in AllTypes
                     .Where(t => !exclusions.Contains(t))
                     .Where(t => !t.Name.EndsWith("Middleware")) // Middlewares can't accept cancellation tokens
                     .Where(t => t.IsClass && !t.IsAbstract && !typeof(Delegate).IsAssignableFrom(t)))
        {
            // Get all public instance and static methods
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .Where(m => typeof(System.Threading.Tasks.Task).IsAssignableFrom(m.ReturnType));

            foreach (var method in methods)
            {
                // 1. Name should end with 'Async'
                if (!method.Name.EndsWith("Async"))
                {
                    failures.Add($"{type.FullName}.{method.Name}: Async method should be suffixed with 'Async'.");
                }

                // 2. Last parameter should be a CT (if there are any parameters)
                var parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters.Last().ParameterType != typeof(CT))
                {
                    failures.Add($"{type.FullName}.{method.Name}: Async method should have a CT as the last parameter.");
                }
            }
        }

        foreach (var failure in failures)
        {
            output.WriteLine(failure);
        }

        failures.ShouldBeEmpty();
    }
    public static bool IsInternal(Type type)
    {
        if (type.IsNested)
        {
            return true;
        }
        return type.IsNestedPrivate || type.IsNotPublic;
    }




#nullable disable
    [Fact]
    public void AccessTokenManagement_is_not_exposed()
    {
        var accessTokenManagementNamespace = typeof(IClientCredentialsTokenManager).Namespace!;
        List<string> errors = new();

        foreach (var type in AllTypes)
        {
            // Only consider public types
            if (!type.IsPublic)
            {
                continue;
            }

            // Check if the type itself is in the forbidden namespace
            if (type.Namespace != null && type.Namespace.StartsWith(accessTokenManagementNamespace))
            {
                errors.Add($"Type {type.FullName} is public and in forbidden namespace.");
            }

            // Check public members for forbidden types
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                switch (member)
                {
                    case MethodInfo method:
                        // Skip implicit and explicit conversion operators
                        if (method.Name == "op_Implicit")
                        {
                            break;
                        }
                        if (IsForbiddenType(method.ReturnType))
                        {
                            errors.Add($"{type.FullName}.{method.Name} returns forbidden type {method.ReturnType.FullName}");
                        }

                        foreach (var param in method.GetParameters())
                        {
                            if (IsForbiddenType(param.ParameterType))
                            {
                                errors.Add($"{type.FullName}.{method.Name} parameter '{param.Name}' is forbidden type {param.ParameterType.FullName}");
                            }
                        }
                        break;
                    case PropertyInfo prop:
                        if (IsForbiddenType(prop.PropertyType))
                        {
                            errors.Add($"{type.FullName}.{prop.Name} property is forbidden type {prop.PropertyType.FullName}");
                        }

                        break;
                    case FieldInfo field:
                        if (IsForbiddenType(field.FieldType))
                        {
                            errors.Add($"{type.FullName}.{field.Name} field is forbidden type {field.FieldType.FullName}");
                        }

                        break;
                    case EventInfo evt:

                        if (IsForbiddenType(evt.EventHandlerType!))
                        {
                            errors.Add($"{type.FullName}.{evt.Name} event is forbidden type {evt.EventHandlerType.FullName}");
                        }

                        break;
                }
            }
        }

        if (errors.Any())
        {
            output.WriteLine("AccessTokenManagement is exposed. Errors found:");
            foreach (var error in errors)
            {
                output.WriteLine(error);
            }

            errors.ShouldBeEmpty(
                "AccessTokenManagement types should not be exposed in BFF public API. Please review the types and ensure they are internal or private.");
        }

        bool IsForbiddenType(Type t)
        {
            if (t.Namespace != null && t.Namespace.StartsWith(accessTokenManagementNamespace))
            {
                return true;
            }

            if (t.IsGenericType)
            {
                foreach (var arg in t.GetGenericArguments())
                {
                    if (IsForbiddenType(arg))
                    {
                        return true;
                    }
                }
            }
            if (t.IsArray)
            {
                return IsForbiddenType(t.GetElementType()!);
            }
            return false;
        }
    }

#nullable enable

    private static List<Type> GetStrongTypedStringTypes()
    {
        // Find all types implementing IStringValue<TSelf>
        var stringValueTypes =
            AllTypes.Where(t => t.IsValueType && !t.IsAbstract)
            .SelectMany(t =>
                t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedValue<>)
                                                && i.GenericTypeArguments[0] == t)
                    .Select(_ => t))
            .Distinct()
            .ToList();
        return stringValueTypes;
    }
    [Fact]
    public void All_interface_async_methods_should_have_cancellation_token_with_default()
    {
        var failures = new List<string>();
        foreach (var type in AllTypes.Where(t => t.IsInterface))
        {
            foreach (var method in type.GetMethods())
            {
                if (typeof(System.Threading.Tasks.Task).IsAssignableFrom(method.ReturnType))
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        failures.Add($"{type.FullName}.{method.Name}: Async method should have a CancellationToken parameter with a default value.");
                        continue;
                    }
                    var ctParam = parameters.Last();
                    if (ctParam.ParameterType != typeof(System.Threading.CancellationToken))
                    {
                        failures.Add($"{type.FullName}.{method.Name}: Last parameter should be CancellationToken.");
                        continue;
                    }
                    if (!ctParam.HasDefaultValue)
                    {
                        failures.Add($"{type.FullName}.{method.Name}: CancellationToken parameter should have a default value.");
                    }
                }
            }
        }

        foreach (var failure in failures)
        {
            output.WriteLine(failure);
        }

        failures.ShouldBeEmpty();
    }
}
