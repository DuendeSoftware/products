// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;

namespace Duende.UserManagement.Profiles;

public interface IUserProfileSelfService
{
    Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct);

    Task<UserProfile?> TryRegisterAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);

    Task<UserProfile?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    Task<UserProfile?> TryUpdateAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);
}
