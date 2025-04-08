// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.Bff.Internal;

using AtmAccessToken = Duende.AccessTokenManagement.AccessToken;

namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Strongly typed representation of an access token. This can be a Client Credentials or User token.
/// </summary>
[JsonConverter(typeof(StringValueJsonConverter<AccessToken>))]
public readonly record struct AccessToken : IStronglyTypedValue<AccessToken>
{
    public override string ToString() => Value;

    // Officially, there's no max length for JWTs, but 32k is a good limit
    public const int MaxLength = 32 * 1024; // 32k

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// Convenience method for converting a <see cref="AccessToken"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(AccessToken value) => value.ToString();
    public static implicit operator AtmAccessToken(AccessToken value) => AtmAccessToken.Parse(value.ToString());

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public AccessToken() => throw new InvalidOperationException("Can't create null value");
    private AccessToken(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out AccessToken? parsed, out string[] errors) =>
        IStronglyTypedValue<AccessToken>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static AccessToken IStronglyTypedValue<AccessToken>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="AccessToken"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static AccessToken Parse(string value) => StringParsers<AccessToken>.Parse(value);
}
