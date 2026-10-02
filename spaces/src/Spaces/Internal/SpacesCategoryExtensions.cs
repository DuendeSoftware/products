// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;

namespace Duende.Spaces.Internal;

internal static class SpacesCategoryExtensions
{
    extension(DataCategoryName)
    {
        internal static DataCategoryName Spaces => DataCategoryName.Create("spaces");
    }
}
