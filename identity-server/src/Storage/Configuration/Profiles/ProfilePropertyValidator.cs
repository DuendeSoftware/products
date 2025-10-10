// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// A builder for validating and potentially overriding a single property value according to profile rules.
/// This provides a fluent API for implementing profile validation logic.
/// </summary>
/// <typeparam name="T">The type of the property being validated.</typeparam>
public class ProfilePropertyValidator<T>
{
    private readonly string _propertyPath;
    private readonly Func<T> _getValue;
    private readonly Action<T> _setValue;
    private readonly ILogger _logger;
    private readonly bool _logOverrides;

    private T _defaultValue = default!;
    private bool _hasDefault;
    private Func<T, bool>? _violationCheck;
    private T? _profileDefaultValue;
    private bool _hasProfileDefault;
    private string? _warningMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilePropertyValidator{T}"/> class.
    /// </summary>
    /// <param name="propertyPath">The path to the property (e.g., "PushedAuthorization.Required").</param>
    /// <param name="getValue">A function to get the current value of the property.</param>
    /// <param name="setValue">An action to set the property value.</param>
    /// <param name="logger">The logger for outputting warnings.</param>
    /// <param name="logOverrides">Whether to log when the profile overrides a value.</param>
    public ProfilePropertyValidator(
        string propertyPath,
        Func<T> getValue,
        Action<T> setValue,
        ILogger logger,
        bool logOverrides)
    {
        _propertyPath = propertyPath;
        _getValue = getValue;
        _setValue = setValue;
        _logger = logger;
        _logOverrides = logOverrides;
    }

    /// <summary>
    /// Specifies the default value of the property as defined in the options class.
    /// This is used to determine if the user has explicitly configured a different value.
    /// </summary>
    /// <param name="defaultValue">The default value of the property.</param>
    /// <returns>The validator for fluent chaining.</returns>
    public ProfilePropertyValidator<T> HasDefault(T defaultValue)
    {
        _defaultValue = defaultValue;
        _hasDefault = true;
        return this;
    }

    /// <summary>
    /// Specifies the rule that determines whether the current value violates the profile requirements.
    /// </summary>
    /// <param name="violationCheck">A function that returns true if the value violates the profile requirements.</param>
    /// <returns>The validator for fluent chaining.</returns>
    public ProfilePropertyValidator<T> ViolatesIf(Func<T, bool> violationCheck)
    {
        _violationCheck = violationCheck;
        return this;
    }

    /// <summary>
    /// Specifies the value that the profile requires if the current value violates the rule.
    /// </summary>
    /// <param name="profileDefaultValue">The value to set if a violation is detected.</param>
    /// <returns>The validator for fluent chaining.</returns>
    public ProfilePropertyValidator<T> OverrideWith(T profileDefaultValue)
    {
        _profileDefaultValue = profileDefaultValue;
        _hasProfileDefault = true;
        return this;
    }

    /// <summary>
    /// Specifies a custom warning message to use when the profile cannot provide a default value.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <returns>The validator for fluent chaining.</returns>
    public ProfilePropertyValidator<T> WarnWith(string message)
    {
        _warningMessage = message;
        return this;
    }

    /// <summary>
    /// Validates the property and applies the profile's requirements.
    /// </summary>
    /// <param name="result">The result object to record passed/failed checks.</param>
    public void Validate(ProfileValidationResult result)
    {
        if (!_hasDefault)
        {
            throw new InvalidOperationException($"HasDefault must be called before Validate for property {_propertyPath}");
        }

        if (_violationCheck == null)
        {
            throw new InvalidOperationException($"ViolatesIf must be called before Validate for property {_propertyPath}");
        }

        var currentValue = _getValue();

        // Check if the current value violates the profile rule
        if (!_violationCheck(currentValue))
        {
            // No violation - record as passed
            result.AddPassed(_propertyPath);
            return;
        }

        // Violation detected
        var isDefaultValue = EqualityComparer<T>.Default.Equals(currentValue, _defaultValue);

        if (_hasProfileDefault)
        {
            // Profile can specify a default value
            if (!isDefaultValue && _logOverrides)
            {
                _logger.LogInformation(
                    "Configuration profile is overriding {PropertyPath} to {ProfileValue}",
                    _propertyPath,
                    _profileDefaultValue);
            }

            _setValue(_profileDefaultValue!);
            result.AddPassed(_propertyPath, wasOverridden: true);
        }
        else
        {
            // Profile cannot specify a default - log warning
            var message = _warningMessage ??
                $"Property {_propertyPath} violates the profile requirements but no default can be provided.";



            if (_logOverrides || !isDefaultValue)
            {
                if (_warningMessage is null)
                {
                    _logger.LogWarning("Property {PropertyPath} violates the profile requirements but no default can be provided.", _propertyPath);
                }
                else
                {
                    _logger.LogWarning("Configuration profile validation warning: {Warning}", _warningMessage);
                }
            }

            result.AddFailed(_propertyPath, message);
        }
    }
}
