// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Globalization;

namespace Duende.Bff.Internal;

internal class StringValueConverter<T> : TypeConverter where T : struct, IParsableType<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string stringValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        return T.Parse(stringValue);
    }
}
