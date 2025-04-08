// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Internal;

namespace Duende.Bff.DynamicFrontends;

[TypeConverter(typeof(StringValueConverter<BffFrontendName>))]
public readonly record struct BffFrontendName : IStronglyTypedValue<BffFrontendName>
{
    /// <summary>
    /// Convenience method for converting a <see cref="BffFrontendName"/> into a string.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator string(BffFrontendName value) => value.ToString();

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(1024)
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public BffFrontendName() => throw new InvalidOperationException("Can't create null value");

    private BffFrontendName(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="BffFrontendName"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out BffFrontendName? parsed, out string[] errors) =>
        IStronglyTypedValue<BffFrontendName>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    /// <summary>
    /// Parses a value to a <see cref="BffFrontendName"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static BffFrontendName Parse(string value) => StringParsers<BffFrontendName>.Parse(value);

    static BffFrontendName IStronglyTypedValue<BffFrontendName>.Create(string result) => new(result);
}

