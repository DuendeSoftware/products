// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Bff.Internal;

/// <summary>
/// Interface for strongly typed objects that wrap a string value.
///
/// This makes sure all these objects have similar methods. Also, it provides
/// a generic way to build them. 
/// </summary>
/// <typeparam name="TSelf"></typeparam>
internal interface IStronglyTypedValue<TSelf> : IParsableType<TSelf>
    where TSelf : struct, IStronglyTypedValue<TSelf>
{
    /// <summary>
    /// Attempt to parse the value object from a string. Return a list of errors if it fails. 
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <param name="parsed">The parsed result</param>
    /// <param name="errors">Errors that occurred during parsing. </param>
    /// <returns>True if parsing was successful</returns>
    static abstract bool TryParse(string value, [NotNullWhen(true)] out TSelf? parsed, out string[] errors);

    /// <summary>
    /// Build an object that represents the string value WITHOUT validation. 
    /// </summary>
    /// <param name="result"></param>
    /// <returns>The build object</returns>
    internal static abstract TSelf Create(string result);

    /// <summary>
    /// Implements validation logic for an object. Also creates the object 
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="validationRules">The validation rules (which are delegates) used to validate the value</param>
    /// <param name="parsed">The parsed object, if validation succeeded. </param>
    /// <param name="foundErrors">The errors that were found. </param>
    /// <returns>True if validation succeeded</returns>
    internal static bool TryBuildValidatedObject(string value, ValidationRule<string>[] validationRules, [NotNullWhen(true)] out TSelf? parsed, out string[] foundErrors)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            foundErrors = ["The string cannot be null or empty."];
            return false;
        }

        List<string> errors = [];
        foreach (var validator in validationRules)
        {
            if (!validator(value, out var message))
            {
                errors.Add(message);
            }
        }

        if (errors.Count == 0)
        {
            parsed = TSelf.Create(value);
        }
        foundErrors = errors.ToArray();

        return foundErrors.Length == 0;
    }
}


internal interface IStronglyTypedValue<TType, TSelf> : IParsableType<TSelf>
    where TType : IParsable<TType>
    where TSelf : struct, IStronglyTypedValue<TType, TSelf>, IParsableType<TSelf>
{


    /// <summary>
    /// Build an object that represents the string value WITHOUT validation. 
    /// </summary>
    /// <param name="result"></param>
    /// <returns>The build object</returns>
    internal static abstract TSelf Create(TType result);

    /// <summary>
    /// Implements validation logic for an object. Also creates the object 
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="formatter"></param>
    /// <param name="validationRules">The validation rules (which are delegates) used to validate the value</param>
    /// <param name="parsed">The parsed object, if validation succeeded. </param>
    /// <param name="foundErrors">The errors that were found. </param>
    /// <returns>True if validation succeeded</returns>
    internal static bool TryBuildValidatedObject(string value, IFormatProvider? formatter, ValidationRule<TType>[] validationRules, [NotNullWhen(true)] out TSelf? parsed, out string[] foundErrors)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            foundErrors = ["The string cannot be null or empty."];
            return false;
        }

        List<string> errors = [];

        if (!TType.TryParse(value, formatter, out var converted))
        {
            errors.Add($"The value '{value}' could not be converted to {typeof(TType).Name}.");
            foundErrors = errors.ToArray();
            return false;
        }

        foreach (var validator in validationRules)
        {
            if (!validator(converted, out var message))
            {
                errors.Add(message);
            }
        }

        if (errors.Count == 0)
        {
            parsed = TSelf.Create(converted);
        }

        foundErrors = errors.ToArray();
        return foundErrors.Length == 0;
    }
}

