// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Linq.Expressions;
using System.Reflection;

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Parses property expressions and creates compiled accessors.
/// This allows profile validators to use expression syntax (opt => opt.Property)
/// instead of explicit getter/setter lambdas.
/// </summary>
internal static class PropertyExpressionParser
{
    /// <summary>
    /// Parses a property expression and returns the property path, getter, and setter.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object (e.g., IdentityServerOptions or Client).</typeparam>
    /// <typeparam name="TProperty">The type of the property value.</typeparam>
    /// <param name="propertyExpression">The expression accessing the property (e.g., opt => opt.PushedAuthorization.Required).</param>
    /// <param name="instance">The instance to bind the accessors to.</param>
    /// <returns>A tuple containing the property path string, getter function, and setter action.</returns>
    public static (string PropertyPath, Func<TProperty> Getter, Action<TProperty> Setter) Parse<TSource, TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression,
        TSource instance)
    {
        // Extract the property path and member info
        var (propertyPath, memberExpression) = ExtractPropertyPath(propertyExpression);

        // Create a compiled getter
        var getter = propertyExpression.Compile();
        TProperty boundGetter() => getter(instance);

        // Create a compiled setter
        var setter = CreateSetter<TSource, TProperty>(memberExpression, instance);

        return (propertyPath, boundGetter, setter);
    }

    /// <summary>
    /// Parses a property expression for IdentityServerOptions (backward compatibility overload).
    /// </summary>
    public static (string PropertyPath, Func<T> Getter, Action<T> Setter) Parse<T>(
        Expression<Func<IdentityServerOptions, T>> propertyExpression,
        IdentityServerOptions instance) => Parse<IdentityServerOptions, T>(propertyExpression, instance);

    private static (string PropertyPath, MemberExpression MemberExpression) ExtractPropertyPath<TSource, T>(
        Expression<Func<TSource, T>> propertyExpression)
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException(
                "Expression must be a property access expression (e.g., opt => opt.Property or opt => opt.Nested.Property)",
                nameof(propertyExpression));
        }

        // Build the property path by walking up the member access chain
        var currentExpression = memberExpression;
        var segments = new Stack<string>();

        while (currentExpression != null)
        {
            segments.Push(currentExpression.Member.Name);
            currentExpression = currentExpression.Expression as MemberExpression;
        }

        // Join the segments with dots (segments are in reverse order due to stack)
        var propertyPath = string.Join(".", segments);

        return (propertyPath, memberExpression);
    }

    private static Action<TProperty> CreateSetter<TSource, TProperty>(MemberExpression memberExpression, TSource instance)
    {
        // Get the property info
        if (memberExpression.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException("Expression must access a property", nameof(memberExpression));
        }

        if (propertyInfo.SetMethod == null)
        {
            throw new ArgumentException($"Property {propertyInfo.Name} does not have a setter", nameof(memberExpression));
        }

        // Build the chain of members from root to target property
        var memberChain = new List<MemberExpression>();
        var current = memberExpression;
        while (current != null)
        {
            memberChain.Add(current);
            current = current.Expression as MemberExpression;
        }

        // Reverse so we go from root to leaf
        memberChain.Reverse();

        // Evaluate the chain to get the object that contains our target property
        object? targetInstance = instance;

        // Walk all but the last member (the last is our target property)
        for (var i = 0; i < memberChain.Count - 1; i++)
        {
            var member = memberChain[i];
            if (member.Member is PropertyInfo pi)
            {
                targetInstance = pi.GetValue(targetInstance);
            }
            else
            {
                throw new ArgumentException("Only property chains are supported", nameof(memberExpression));
            }
        }

        // Now we have the object containing the target property
        // Get the type from the last intermediate member, or use TSource if no intermediates
        var targetType = memberChain.Count > 1
            ? memberChain[memberChain.Count - 2].Type
            : typeof(TSource);

        // Create a setter: (value) => target.Property = value
        var valueParameter = Expression.Parameter(typeof(TProperty), "value");
        var targetExpression = Expression.Constant(targetInstance, targetType);
        var propertyAccess = Expression.Property(targetExpression, propertyInfo);
        var assignment = Expression.Assign(propertyAccess, valueParameter);
        var setterLambda = Expression.Lambda<Action<TProperty>>(assignment, valueParameter);

        return setterLambda.Compile();
    }
}
