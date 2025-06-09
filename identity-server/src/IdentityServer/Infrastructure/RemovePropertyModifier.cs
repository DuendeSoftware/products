// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization.Metadata;

namespace Duende.IdentityServer.Infrastructure;

public class RemovePropertyModifier<T>(List<string> propertiesToRemove)
{
    public void ModifyTypeInfo(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(T))
        {
            return;
        }

        var propsToKeep = typeInfo.Properties.Where(propertyInfo => !propertiesToRemove.Contains(propertyInfo.Name)).ToArray();

        typeInfo.Properties.Clear();
        foreach (var prop in propsToKeep)
        {
            typeInfo.Properties.Add(prop);
        }
    }
}
