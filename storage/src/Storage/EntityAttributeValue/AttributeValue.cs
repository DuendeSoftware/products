// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents a read-only, validated attribute value paired with its attribute code.
///     Instances can only be created via an <see cref="AttributeValueCollection"/> (which validates against a schema)
///     or by reconstituting from previously persisted data using <see cref="Load{T}"/>.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public abstract record AttributeValue
{
    private protected AttributeValue(AttributeCode code) => Code = code;

    /// <summary>
    ///     The attribute code identifying which attribute this value belongs to.
    /// </summary>
    public AttributeCode Code { get; }

    /// <summary>
    ///     Gets the value as an untyped object.
    /// </summary>
    public abstract object UntypedValue { get; }

    /// <summary>
    ///     Attempts to retrieve the value as the specified type.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="value">The typed value if successful.</param>
    /// <returns><c>true</c> if the value is of the specified type; otherwise, <c>false</c>.</returns>
    public bool TryGetValue<T>([MaybeNullWhen(false)] out T value)
    {
        if (this is AttributeValue<T> typed)
        {
            value = typed.TypedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    ///     Returns the string representation of the value.
    /// </summary>
    public override string ToString() => UntypedValue.ToString()!;

    /// <summary>
    ///     Reconstitutes a typed attribute value from persisted data.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="code">The attribute code.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>A new typed <see cref="AttributeValue{T}"/> instance.</returns>
    public static AttributeValue<T> Load<T>(AttributeCode code, T value) => new(code, value);
}

/// <summary>
///     Represents a strongly-typed attribute value paired with its attribute code.
/// </summary>
/// <typeparam name="T">The type of the attribute value.</typeparam>
public sealed record AttributeValue<T> : AttributeValue
{
    internal AttributeValue(AttributeCode code, T value) : base(code) =>
        TypedValue = value switch
        {
            IReadOnlyDictionary<string, object> dict => (T)(object)new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(dict)),
            IReadOnlyList<object> list => (T)(object)list.ToList().AsReadOnly(),
            _ => value
        };

    /// <summary>
    ///     Gets the strongly-typed value.
    /// </summary>
    public T TypedValue { get; }

    /// <summary>
    ///     Gets the value as an untyped object.
    /// </summary>
    public override object UntypedValue => TypedValue!;

    internal static AttributeValue<T> Load(AttributeCode code, T value) => new(code, value);
}
