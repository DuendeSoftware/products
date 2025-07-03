// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Internal;

namespace Duende.Bff.SessionManagement.SessionStore;

public readonly record struct PartitionKey : IStronglyTypedValue<PartitionKey>
{
    public override string ToString() => Value;

    // This is the default length in the db
    public const int MaxLength = 200;

    private static readonly ValidationRule<string>[] Validators =
    [
        ValidationRules.MaxLength(MaxLength)
    ];

    /// <summary>
    /// You can't directly create this type. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public PartitionKey() => throw new InvalidOperationException("Can't create null value");

    private PartitionKey(string value) => Value = value;

    private string Value { get; }

    /// <summary>
    /// Parses a value to a <see cref="PartitionKey"/>. This method will return false if the value is invalid
    /// and also includes a list of errors. This is useful for validating user input or other scenarios where you want to provide feedback
    /// </summary>
    public static bool TryParse(string value, [NotNullWhen(true)] out PartitionKey? parsed, out string[] errors) =>
        IStronglyTypedValue<PartitionKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);

    static PartitionKey IStronglyTypedValue<PartitionKey>.Create(string result) => new(result);

    /// <summary>
    /// Parses a value to a <see cref="PartitionKey"/>. This will throw an exception if the string is not valid.
    /// </summary>
    public static PartitionKey Parse(string value) => StringParsers<PartitionKey>.Parse(value);
}
