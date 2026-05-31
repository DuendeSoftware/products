// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Admin;

/// <summary>
/// Represents a strongly-typed version number used for optimistic concurrency control.
/// </summary>
[ValueOf<int>]
public partial record DataVersion
{
}
