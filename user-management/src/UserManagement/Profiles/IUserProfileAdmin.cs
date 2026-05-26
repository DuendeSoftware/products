// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Querying;

namespace Duende.UserManagement.Profiles;

public interface IUserProfileAdmin
{
    Task<IReadOnlyAttributeSchema> GetSchemaAsync(Ct ct);

    Task<UserProfile?> TryAddAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection attributes, Ct ct);

    Task<UserProfile?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    Task<UserProfile?> TryGetAsync(AttributeCode attributeCode, object value, Ct ct);

    Task<QueryResult<UserProfile>> QueryAsync(
        QueryRequest request,
        Ct ct);

    Task<QueryResult<UserProfileAttributeProjection>> QueryAsync(
        QueryRequest request,
        HashSet<AttributeCode> attributes,
        Ct ct);
}
