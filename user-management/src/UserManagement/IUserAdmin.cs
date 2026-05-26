// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

public interface IUserAdmin
{
    Task<bool> TrySetUserNameAsync(UserSubjectId subjectId, UserName userName, Ct ct);

    Task<bool> TryRemoveUserNameAsync(UserSubjectId subjectId, Ct ct);

    Task<bool> TryRemoveAsync(UserSubjectId subjectId, Ct ct);
}
