// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

/// <summary>
/// Represents a JSON Web Key Set.
/// </summary>
/// <remarks>
/// The keys themselves are represented as objects without additional structure,
/// rather than more complex types, such as 
/// <see cref="IdentityModel.Jwk.JsonWebKey" />, because we don't want
/// serializing and deserializing to and from such types to introduce additional
/// properties to the keys.
/// </remarks>
public record KeySet(IEnumerable<object> Keys);
