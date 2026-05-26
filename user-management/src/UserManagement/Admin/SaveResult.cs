// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Admin;

public static class SaveResult
{
    public static SaveResult<TId> Success<TId>(TId id, DataVersion version) where TId : notnull =>
        new()
        {
            IsSuccess = true,
            Id = id,
            Version = version
        };

    public static SaveResult<TId> Failure<TId>(params AdminError[] errors) where TId : notnull =>
        new()
        {
            IsSuccess = false,
            Errors = errors
        };
}

public record SaveResult<TId> where TId : notnull
{
    [MemberNotNullWhen(true, nameof(Id), nameof(Version))]
    [MemberNotNullWhen(false, nameof(Errors))]
    public bool IsSuccess { get; internal set; }

    public TId? Id { get; internal set; }
    public DataVersion? Version { get; internal set; }
    public IReadOnlyList<AdminError>? Errors { get; internal set; }

#pragma warning disable CA2225
    public static implicit operator SaveResult<TId>((TId Id, DataVersion version) success) => new()
    {
        IsSuccess = true,
        Id = success.Id,
        Version = success.version
    };

    public static implicit operator SaveResult<TId>(AdminError[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors
    };

    public static implicit operator SaveResult<TId>(AdminError error) => new()
    {
        IsSuccess = false,
        Errors = [error]
    };

    public override string ToString() => IsSuccess
        ? $"Saved {typeof(TId).Name} with id {Id}, Version {Version}"
        : $"Failed to save {typeof(TId).Name}. Errors: {string.Join(' ', Errors.Select(x => "\n - " + x))}";
#pragma warning restore CA2225
}
