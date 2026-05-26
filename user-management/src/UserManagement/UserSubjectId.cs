// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

[StringValue]
public partial record UserSubjectId : ISubjectId
{
    internal const int MaxLength = 200;

    public static UserSubjectId New() => Create(Guid.NewGuid().ToString());
}
