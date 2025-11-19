// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Internal;

internal interface IParsableType<out TSelf> where TSelf : struct, IParsableType<TSelf>
{
    /// <summary>
    /// Parse the value object from a string. This method throws if validation fails and should be done when the
    /// only course of action for invalid values is to throw anyway. 
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <returns>The parsed object</returns>
    public static abstract TSelf Parse(string value);
}
