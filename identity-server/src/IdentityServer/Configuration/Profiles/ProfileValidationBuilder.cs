// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// A helper class for building profile property validators.
/// This provides a fluent API for validating properties according to profile rules.
/// </summary>
/// <typeparam name="TSource">The type of object being validated (e.g., IdentityServerOptions or Client).</typeparam>
public class ProfileValidationBuilder<TSource>
{
    private readonly TSource _instance;
    private readonly ILogger _logger;
    private readonly bool _logOverrides;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileValidationBuilder{TSource}"/> class.
    /// </summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="logger">The logger for outputting warnings.</param>
    /// <param name="logOverrides">Whether to log when the profile overrides a value.</param>
    public ProfileValidationBuilder(TSource instance, ILogger logger, bool logOverrides)
    {
        _instance = instance;
        _logger = logger;
        _logOverrides = logOverrides;
    }

    /// <summary>
    /// Creates a validator for a property using an expression.
    /// The property path is automatically derived from the expression.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertyExpression">An expression that accesses the property (e.g., opt => opt.PushedAuthorization.Required).</param>
    /// <returns>A property validator for fluent configuration.</returns>
    public ProfilePropertyValidator<TProperty> Property<TProperty>(Expression<Func<TSource, TProperty>> propertyExpression)
    {
        var (propertyPath, getter, setter) = PropertyExpressionParser.Parse(propertyExpression, _instance);

        return new ProfilePropertyValidator<TProperty>(
            propertyPath,
            getter,
            setter,
            _logger,
            _logOverrides);
    }

    /// <summary>
    /// Creates a validator for a property.
    /// </summary>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="propertyPath">The path to the property (e.g., "PushedAuthorization.Required" or "JwtValidationClockSkew").</param>
    /// <param name="getValue">A function to get the current value of the property.</param>
    /// <param name="setValue">An action to set the property value.</param>
    /// <returns>A property validator for fluent configuration.</returns>
    public ProfilePropertyValidator<TProperty> Property<TProperty>(
        string propertyPath,
        Func<TSource, TProperty> getValue,
        Action<TSource, TProperty> setValue) => new ProfilePropertyValidator<TProperty>(
            propertyPath,
            () => getValue(_instance),
            value => setValue(_instance, value),
            _logger,
            _logOverrides);
}
