// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.SessionManagement.SessionStore;

/// <summary>
/// Retrieves a user session partition key based on the current HTTP context and application discriminator.
/// </summary>
/// <returns></returns>
public delegate PartitionKey BuildUserSessionPartitionKey();
