// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Internal;

internal record Result<TValue, TFailure>
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    internal bool Success { get; private init; }
    internal TValue? Value { get; private init; }
    internal TFailure? Error { get; private init; }

    public static Result<TValue, TFailure> FromValue(TValue value) =>
        new()
        {
            Success = true,
            Value = value
        };

    public static Result<TValue, TFailure> FromError(TFailure error) =>
        new()
        {
            Success = false,
            Error = error
        };

    public static implicit operator Result<TValue, TFailure>(TFailure value) =>
        FromError(value);
    public static implicit operator Result<TValue, TFailure>(TValue value) =>
        FromValue(value);

    // Note: We can't have an implicit operator for TFailure when it's an interface
    // because C# won't do double conversion (ConcreteType -> Interface -> Result)

    public static Result.ResultData<TData> Create<TData>(TData item) => new(item);
    public static implicit operator Result<TValue, TFailure>(Result.ResultData<TValue> value) => FromValue(value.Data);
    public static implicit operator Result<TValue, TFailure>(Result.ResultData<TFailure> value) => FromError(value.Data);
}

internal static class Result
{
    public static ResultData<T> Create<T>(T item) => new(item);
    internal record ResultData<TData>(TData Data);
}
