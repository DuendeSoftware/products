// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.UserManagement.Internal.Storage;

/// <summary>
/// Extension members for <see cref="DataCategoryName"/>, for usage by Duende Software products.
/// </summary>
public static class DataCategoryNameExtensions
{
    extension(DataCategoryName)
    {
        /// <summary>
        /// The storage category used for User Management data.
        /// </summary>
        public static DataCategoryName UserManagement => DataCategoryName.Create("user-management");
    }
}
