// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.Bff.Internal;
using Duende.IdentityModel;

using AtmScheme = Duende.AccessTokenManagement.Scheme;

namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Represents an OIDC scheme. This is a strongly typed value object that validates the string value.
/// This will also normalize the casing of the scheme. This is not officially part of the spec, but some
/// OP's are case-sensitive in their scheme handling.
/// </summary>
[TypeConverter(typeof(StringValueConverter<Scheme>))]
[JsonConverter(typeof(StringValueJsonConverter<Scheme>))]
public readonly record struct Scheme : IStronglyTypedValue<Scheme>
{
    public const int MaxLength = 50;

    /// <summary>
    /// Convenience method for converting a <see cref="Scheme"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(Scheme value) => value.ToString();
    public static implicit operator AtmScheme(Scheme value) => AtmScheme.Parse(value.ToString());

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
    ];

    public static readonly Scheme
        Bearer = Parse(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer);

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public Scheme() => throw new InvalidOperationException("Can't create null value");

    private Scheme(string value)
    {

        // Some target systems are case-sensitive in their scheme handling. This code normalizes
        // the casing.

        // since AccessTokenType above in the token endpoint response (the token_type value) could be case-insensitive, but
        // when we send it as an Authorization header in the API request it must be case-sensitive, we
        // are checking for that here and forcing it to the exact casing required.

        //IE: if Scheme == BeAReR => "Bearer"
        //IE: if Scheme == DpoP => "DPoP"
        if (value.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, StringComparison.OrdinalIgnoreCase))
        {
            value = OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;
        }
        else if (value.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, StringComparison.OrdinalIgnoreCase))
        {
            value = OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP;
        }
        Value = value;
    }

    private string Value { get; }

    /// <summary>
    /// Used to represent an empty scheme for caching purposes
    /// </summary>
    internal static Scheme Empty = new(string.Empty);

    /// <summary>
    /// Parses a value to a <see cref="Scheme"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out Scheme? parsed, out string[] errors) =>
        IStronglyTypedValue<Scheme>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static Scheme IStronglyTypedValue<Scheme>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="Scheme"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static Scheme Parse(string value) => StringParsers<Scheme>.Parse(value);
    public static Scheme? ParseOrDefault(string? value) => StringParsers<Scheme>.ParseOrDefault(value);

}
