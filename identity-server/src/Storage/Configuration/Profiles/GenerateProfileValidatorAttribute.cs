// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Marks a type as needing a generated profile validator.
/// The source generator will create a validator class with strongly-typed property accessors.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
public sealed class GenerateProfileValidatorAttribute : System.Attribute
{
}
