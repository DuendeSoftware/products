// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Duende.IdentityServer.Stores.Serialization;

/// <summary>
/// A <see cref="IJsonTypeInfoResolver"/> that supports runtime-extensible polymorphic
/// JSON serialization. Derived types and their discriminator values are registered at
/// startup rather than via compile-time attributes, making it possible for user code
/// to add custom derived types (e.g., custom <see cref="Models.IdentityProvider"/> subtypes).
/// </summary>
internal class PolymorphicJsonTypeResolver : DefaultJsonTypeInfoResolver
{
    private readonly Dictionary<Type, PolymorphicTypeRegistration> _registrations = new();

    /// <summary>
    /// Registers a base type for polymorphic serialization and returns a registration
    /// object that can be used to add derived types.
    /// </summary>
    /// <typeparam name="TBase">The base type to register.</typeparam>
    /// <param name="typeDiscriminatorPropertyName">The JSON property name used as the type discriminator.</param>
    /// <returns>A <see cref="PolymorphicTypeRegistration"/> for adding derived types.</returns>
    public PolymorphicTypeRegistration AddPolymorphicType<TBase>(string typeDiscriminatorPropertyName)
    {
        var registration = new PolymorphicTypeRegistration(typeof(TBase), typeDiscriminatorPropertyName);
        _registrations[typeof(TBase)] = registration;
        return registration;
    }

    /// <inheritdoc />
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var typeInfo = base.GetTypeInfo(type, options);

        if (_registrations.TryGetValue(type, out var registration))
        {
            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = registration.TypeDiscriminatorPropertyName,
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
            };

            foreach (var (derivedType, discriminator) in registration.DerivedTypes)
            {
                typeInfo.PolymorphismOptions.DerivedTypes.Add(
                    new JsonDerivedType(derivedType, discriminator));
            }
        }

        return typeInfo;
    }
}

/// <summary>
/// Holds the configuration for a single polymorphic base type, including its
/// discriminator property name and the set of derived types.
/// </summary>
internal class PolymorphicTypeRegistration
{
    internal Type BaseType { get; }
    internal string TypeDiscriminatorPropertyName { get; }
    internal List<(Type DerivedType, string Discriminator)> DerivedTypes { get; } = new();

    internal PolymorphicTypeRegistration(Type baseType, string typeDiscriminatorPropertyName)
    {
        BaseType = baseType;
        TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName;
    }

    /// <summary>
    /// Adds a derived type with the specified discriminator value.
    /// </summary>
    /// <typeparam name="TDerived">The derived type.</typeparam>
    /// <param name="discriminator">The discriminator value used in JSON.</param>
    /// <returns>This registration for chaining.</returns>
    public PolymorphicTypeRegistration AddDerivedType<TDerived>(string discriminator) where TDerived : class =>
        AddDerivedType(typeof(TDerived), discriminator);

    /// <summary>
    /// Adds a derived type with the specified discriminator value.
    /// </summary>
    /// <param name="derivedType">The derived type.</param>
    /// <param name="discriminator">The discriminator value used in JSON.</param>
    /// <returns>This registration for chaining.</returns>
    public PolymorphicTypeRegistration AddDerivedType(Type derivedType, string discriminator)
    {
        DerivedTypes.Add((derivedType, discriminator));
        return this;
    }
}
