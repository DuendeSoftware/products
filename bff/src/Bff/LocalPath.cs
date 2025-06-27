// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Internal;
using Microsoft.AspNetCore.Http;

namespace Duende.Bff;

[TypeConverter(typeof(StringValueConverter<LocalPath>))]
[JsonConverter(typeof(StringValueJsonConverter<LocalPath>))]
public readonly record struct LocalPath : IStronglyTypedValue<LocalPath>
{
    /// <summary>
    /// Convenience method to parse a string into a <see cref="BffFrontendName"/>.
    /// This will throw an exception if the string is not valid. If you wish more control
    /// over the conversion process, please use <see cref="TryParse"/> or <see cref="Parse"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static implicit operator LocalPath(PathString value) => ToLocalPath(value);

    /// <summary>
    /// Convenience method for converting a <see cref="LocalPath"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(LocalPath value) => value.ToString();

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public LocalPath() => throw new InvalidOperationException("Can't create null value");
    private LocalPath(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="LocalPath"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out LocalPath? parsed, out string[] errors) =>
        IStronglyTypedValue<LocalPath>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    public static LocalPath ToLocalPath(PathString pathString) => Parse(pathString);

    /// <summary>
    /// Parses a value to a <see cref="LocalPath"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static LocalPath Parse(string value) => StringParsers<LocalPath>.Parse(value);

    static LocalPath IStronglyTypedValue<LocalPath>.Create(string result) => new(result);

}

