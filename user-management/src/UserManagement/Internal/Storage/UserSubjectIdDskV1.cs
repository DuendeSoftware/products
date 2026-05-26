// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Internal.Storage;

internal sealed record UserSubjectIdDskV1 : IDataStorageKey
{
    private UserSubjectIdDskV1(string value) => Value = value;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(UserRepository.Keys.SubjectId, 1);

    public string Value { get; }

    public static UserSubjectIdDskV1 Create(UserSubjectId userSubjectId) => new(userSubjectId.Value);
}
