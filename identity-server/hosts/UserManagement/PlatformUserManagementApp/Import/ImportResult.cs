// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace PlatformUserManagementApp.Import;

/// <summary>
/// Summary of a user import operation.
/// </summary>
/// <param name="Created">Number of users newly created.</param>
/// <param name="Overwritten">Number of existing users overwritten.</param>
/// <param name="Failed">Number of users that failed to import.</param>
/// <param name="Errors">List of error messages for failed imports.</param>
public sealed record ImportResult(int Created, int Overwritten, int Failed, IReadOnlyList<string> Errors)
{
    /// <summary>Total number of users successfully imported (created + overwritten).</summary>
    public int Imported => Created + Overwritten;

    public static ImportResult Empty => new(0, 0, 0, []);
}
