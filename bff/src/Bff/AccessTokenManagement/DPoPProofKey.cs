// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.Bff.Internal;
using Microsoft.IdentityModel.Tokens;
using AtmDPoPProofKey = Duende.AccessTokenManagement.DPoP.DPoPProofKey;

namespace Duende.Bff.AccessTokenManagement;

[TypeConverter(typeof(StringValueConverter<DPoPProofKey>))]
[JsonConverter(typeof(StringValueJsonConverter<DPoPProofKey>))]
public readonly record struct DPoPProofKey : IStronglyTypedValue<DPoPProofKey>
{
    public bool Equals(DPoPProofKey other) => Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Implicitly converts DPOPProofKey to same type from access token management.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator AtmDPoPProofKey(DPoPProofKey value) => AtmDPoPProofKey.Parse(value.ToString());

    /// <summary>
    /// Convenience method for converting a <see cref="DPoPProofKey"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(DPoPProofKey value) => value.ToString();

    private readonly JsonWebKey _jsonWebKey;

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(32 * 1024),
        IsValidJsonWebKey()
    ];

    private static ValidationRule<string> IsValidJsonWebKey() =>
        (string value, out string message) =>
        {
            message = string.Empty;
            try
            {
                JsonWebKey.Create(value);
                return true;
            }
            catch (Exception e)
            {
                message = "String is not a valid json web key: " + e.Message;
                return false;
            }
        };

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public DPoPProofKey() => throw new InvalidOperationException("Can't create null value");
    private DPoPProofKey(string value)
    {
        Value = value;
        _jsonWebKey = JsonWebKey.Create(value);
    }

    private string Value { get; }

    /// <summary>
    /// Converts the proof key into a <see cref="JsonWebKey"/>. 
    /// </summary>
    /// <returns></returns>
    public JsonWebKey ToJsonWebKey() => _jsonWebKey;

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPProofKey? parsed, out string[] errors) =>
        IStronglyTypedValue<DPoPProofKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static DPoPProofKey IStronglyTypedValue<DPoPProofKey>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static DPoPProofKey Parse(string value) => StringParsers<DPoPProofKey>.Parse(value);

    /// <summary>
    /// Parses a value to a <see cref="DPoPProofKey"/>. This will return null if the provided string
    /// is null or whitespace. This is a convenience method for when you want to parse a value that may
    /// contain null or whitespace strings. 
    /// </summary>
    public static DPoPProofKey? ParseOrDefault(string? value) => StringParsers<DPoPProofKey>.ParseOrDefault(value);

}
