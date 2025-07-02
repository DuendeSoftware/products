// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.SessionManagement.SessionStore;

public readonly record struct UserSessionKey(PartitionKey PartitionKey, UserKey UserKey)
{
    public override string ToString() => $"{PartitionKey}|{UserKey}";
}
