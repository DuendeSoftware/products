// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

public static class StoreTypeExtensions
{
    extension(StoreType type)
    {
        public TheoryDataRow<StoreType> ToTheoryDataRow() =>
            new(type) { Traits = { [nameof(StoreType)] = [type.ToString()] } };
    }
}
