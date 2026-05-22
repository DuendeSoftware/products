// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Filtering.Expressions;
using Duende.Storage.Querying;

namespace Duende.Storage.Internal.Filtering;

/// <summary>
/// Parses SCIM-style filter strings into a filter expression tree.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class FilterExpressionParser
{
    /// <summary>
    /// Parses a filter string into a <see cref="FilterExpression"/>.
    /// </summary>
    /// <param name="filter">The SCIM filter string to parse.</param>
    /// <returns>The parsed filter expression tree.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filter"/> is null or whitespace.</exception>
    /// <exception cref="FilterParseException">Thrown when the filter string has invalid syntax.</exception>
    public static FilterExpression Parse(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            throw new ArgumentException("Filter cannot be null or whitespace.", nameof(filter));
        }

        try
        {
            var tokens = FilterLexer.Tokenize(filter);
            var parser = new Parser(tokens, filter);
            var expression = parser.ParseFilter();

            if (!parser.IsAtEnd)
            {
                var current = parser.Peek();
                throw new FilterParseException(
                    $"Unexpected token '{current.Value}' at position {current.Position}; expected end of input");
            }

            return expression;
        }
        catch (Exception ex) when (ex is not FilterParseException and not ArgumentException)
        {
            throw new FilterParseException($"Invalid filter syntax: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to parse a filter string, returning a value indicating success.
    /// </summary>
    /// <param name="filter">The SCIM filter string to parse.</param>
    /// <param name="expression">When successful, the parsed filter expression; otherwise, null.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string filter, out FilterExpression? expression)
    {
        try
        {
            expression = Parse(filter);
            return true;
        }
#pragma warning disable CA1031 // TryParse is designed to catch all exceptions
        catch (Exception)
#pragma warning restore CA1031
        {
            expression = null;
            return false;
        }
    }

    private sealed class Parser(List<LexToken> tokens, string input)
    {
        private const int MaxDepth = 100;
        private int _position;
        private int _depth;

        internal bool IsAtEnd => _position >= tokens.Count;

        internal LexToken Peek()
        {
            if (IsAtEnd)
            {
                return new LexToken(FilterToken.None, string.Empty, input.Length);
            }
            return tokens[_position];
        }

        private LexToken Advance()
        {
            if (IsAtEnd)
            {
                throw new FilterParseException("Unexpected end of input");
            }
            return tokens[_position++];
        }

        private LexToken Expect(FilterToken type)
        {
            var token = Peek();
            if (token.Type != type)
            {
                var expected = type.ToString();
                if (IsAtEnd)
                {
                    throw new FilterParseException(
                        $"Unexpected end of input; expected {expected}");
                }
                throw new FilterParseException(
                    $"Unexpected token '{token.Value}' at position {token.Position}; expected {expected}");
            }
            return Advance();
        }

        private bool Check(FilterToken type) => !IsAtEnd && Peek().Type == type;

        internal FilterExpression ParseFilter() => ParseOrExpression();

        private FilterExpression ParseOrExpression()
        {
            var left = ParseAndExpression();
            while (Check(FilterToken.Or))
            {
                _ = Advance();
                var right = ParseAndExpression();
                left = new LogicalExpression(LogicalOperator.Or, left, right);
            }
            return left;
        }

        private FilterExpression ParseAndExpression()
        {
            var left = ParseUnaryExpression();
            while (Check(FilterToken.And))
            {
                _ = Advance();
                var right = ParseUnaryExpression();
                left = new LogicalExpression(LogicalOperator.And, left, right);
            }
            return left;
        }

        private FilterExpression ParseUnaryExpression()
        {
            if (Check(FilterToken.Not))
            {
                _ = Advance();
                var expr = ParsePrimaryExpression();
                return new LogicalExpression(LogicalOperator.Not, expr);
            }
            return ParsePrimaryExpression();
        }

        private FilterExpression ParsePrimaryExpression()
        {
            if (Check(FilterToken.LParen))
            {
                return ParseGroupedExpression();
            }
            return ParseAttributeExpression();
        }

        private FilterExpression ParseGroupedExpression()
        {
            _ = Expect(FilterToken.LParen);
            if (++_depth > MaxDepth)
            {
                throw new FilterParseException(
                    $"Filter expression exceeds maximum nesting depth of {MaxDepth}");
            }
            try
            {
                var expr = ParseFilter();
                _ = Expect(FilterToken.RParen);
                return expr;
            }
            finally
            {
                _depth--;
            }
        }

        private FilterExpression ParseAttributeExpression()
        {
            var attrPath = ParseAttributePath();

            // Complex attribute filter: emails[type eq "work"]
            if (Check(FilterToken.LBracket))
            {
                return ParseComplexFilter(attrPath);
            }

            // Present operator: title pr
            if (Check(FilterToken.Pr))
            {
                _ = Advance();
                return new ComparisonExpression(attrPath, ComparisonOperator.Present, null);
            }

            // Comparison: attrPath op value
            if (IsComparisonOperator(Peek().Type))
            {
                var op = ParseComparisonOperator();
                var value = ParseCompValue();
                return new ComparisonExpression(attrPath, op, value);
            }

            var current = Peek();
            if (IsAtEnd)
            {
                throw new FilterParseException("Unexpected end of input; expected operator");
            }
            throw new FilterParseException(
                $"Unexpected token '{current.Value}' at position {current.Position}; expected operator");
        }

        private ComplexAttributeExpression ParseComplexFilter(AttributePathExpression attrPath)
        {
            _ = Expect(FilterToken.LBracket);
            if (++_depth > MaxDepth)
            {
                throw new FilterParseException(
                    $"Filter expression exceeds maximum nesting depth of {MaxDepth}");
            }
            try
            {
                var filter = ParseFilter();
                _ = Expect(FilterToken.RBracket);
                return new ComplexAttributeExpression(attrPath, filter);
            }
            finally
            {
                _depth--;
            }
        }

        private AttributePathExpression ParseAttributePath()
        {
            var first = Expect(FilterToken.Identifier).Value;

            while (Check(FilterToken.Dot) || Check(FilterToken.Colon))
            {
                if (Check(FilterToken.Dot))
                {
                    // Standard sub-attribute: attrName.subAttr
                    _ = Advance();
                    first += "." + Expect(FilterToken.Identifier).Value;
                }
                else // Colon
                {
                    _ = Advance();
                    if (Check(FilterToken.Identifier))
                    {
                        first += ":" + Advance().Value;
                    }
                    else if (Check(FilterToken.Number))
                    {
                        // Version segment in URI, e.g. "2" or "2.0" in "core:2.0:User"
                        first += ":" + Advance().Value;
                    }
                    else
                    {
                        var current = Peek();
                        throw new FilterParseException(
                            $"Unexpected token '{current.Value}' at position {current.Position}; expected identifier or number in attribute path");
                    }
                }
            }

            return new AttributePathExpression(first);
        }

        private static bool IsComparisonOperator(FilterToken type) =>
            type is FilterToken.Eq or FilterToken.Ne or FilterToken.Co or FilterToken.Sw
                or FilterToken.Ew or FilterToken.Gt or FilterToken.Ge or FilterToken.Lt
                or FilterToken.Le;

        private ComparisonOperator ParseComparisonOperator()
        {
            var token = Advance();
            return token.Type switch
            {
                FilterToken.Eq => ComparisonOperator.Equal,
                FilterToken.Ne => ComparisonOperator.NotEqual,
                FilterToken.Co => ComparisonOperator.Contains,
                FilterToken.Sw => ComparisonOperator.StartsWith,
                FilterToken.Ew => ComparisonOperator.EndsWith,
                FilterToken.Gt => ComparisonOperator.GreaterThan,
                FilterToken.Ge => ComparisonOperator.GreaterThanOrEqual,
                FilterToken.Lt => ComparisonOperator.LessThan,
                FilterToken.Le => ComparisonOperator.LessThanOrEqual,
                _ => throw new FilterParseException(
                    $"Unexpected token '{token.Value}' at position {token.Position}; expected comparison operator")
            };
        }

        private object? ParseCompValue()
        {
            var token = Peek();
            switch (token.Type)
            {
                case FilterToken.StringLiteral:
                    _ = Advance();
                    return UnescapeString(token.Value);

                case FilterToken.Number:
                    _ = Advance();
                    return ParseNumber(token.Value, token.Position);

                case FilterToken.True:
                    _ = Advance();
                    return true;

                case FilterToken.False:
                    _ = Advance();
                    return false;

                case FilterToken.Null:
                    _ = Advance();
                    return null;

                default:
                    if (IsAtEnd)
                    {
                        throw new FilterParseException("Unexpected end of input; expected value");
                    }
                    throw new FilterParseException(
                        $"Unexpected token '{token.Value}' at position {token.Position}; expected value");
            }
        }

        private static string UnescapeString(string raw)
        {
            // Strip surrounding quotes
            var inner = raw[1..^1];

            if (!inner.Contains('\\', StringComparison.Ordinal))
            {
                return inner;
            }

            var result = new System.Text.StringBuilder(inner.Length);
            for (var i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '\\' && i + 1 < inner.Length)
                {
                    i++;
                    _ = result.Append(inner[i]);
                }
                else
                {
                    _ = result.Append(inner[i]);
                }
            }
            return result.ToString();
        }

        private static object ParseNumber(string text, int position)
        {
            try
            {
                if (text.Contains('.', StringComparison.Ordinal))
                {
                    return double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var i))
                {
                    return i;
                }

                if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var l))
                {
                    return l;
                }

                throw new FormatException($"Cannot parse number: {text}");
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new FilterParseException(
                    $"Invalid number '{text}' at position {position}",
                    ex);
            }
        }
    }
}
