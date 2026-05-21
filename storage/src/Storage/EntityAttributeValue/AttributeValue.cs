// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public abstract record AttributeValue
{
    private protected AttributeValue(AttributeCode code) => Code = code;

    public AttributeCode Code { get; }

    public abstract object UntypedValue { get; }

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

    public override string ToString() => UntypedValue.ToString()!;

    public static AttributeValue<T> Load<T>(AttributeCode code, T value) => new(code, value);
}

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

    public T TypedValue { get; }

    public override object UntypedValue => TypedValue!;

    internal static AttributeValue<T> Load(AttributeCode code, T value) => new(code, value);
}
