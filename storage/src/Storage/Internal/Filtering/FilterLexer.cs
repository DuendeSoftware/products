// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;

namespace Duende.Storage.Internal.Filtering;

internal static class FilterLexer
{
    public static List<LexToken> Tokenize(string input)
    {
        var tokens = new List<LexToken>();
        var position = 0;

        while (position < input.Length)
        {
            var ch = input[position];

            // Skip whitespace
            if (char.IsWhiteSpace(ch))
            {
                position++;
                continue;
            }

            // Single-character punctuation
            switch (ch)
            {
                case '(':
                    tokens.Add(new LexToken(FilterToken.LParen, "(", position));
                    position++;
                    continue;
                case ')':
                    tokens.Add(new LexToken(FilterToken.RParen, ")", position));
                    position++;
                    continue;
                case '[':
                    tokens.Add(new LexToken(FilterToken.LBracket, "[", position));
                    position++;
                    continue;
                case ']':
                    tokens.Add(new LexToken(FilterToken.RBracket, "]", position));
                    position++;
                    continue;
                case '.':
                    tokens.Add(new LexToken(FilterToken.Dot, ".", position));
                    position++;
                    continue;
                case ':':
                    tokens.Add(new LexToken(FilterToken.Colon, ":", position));
                    position++;
                    continue;
            }

            // String literal
            if (ch == '"')
            {
                var start = position;
                position++; // skip opening quote
                while (position < input.Length)
                {
                    if (input[position] == '\\')
                    {
                        position += 2; // skip escape sequence
                        continue;
                    }
                    if (input[position] == '"')
                    {
                        break;
                    }
                    position++;
                }

                if (position >= input.Length)
                {
                    throw new FilterParseException(
                        $"Unterminated string literal at position {start}");
                }

                position++; // skip closing quote
                var value = input[start..position];
                tokens.Add(new LexToken(FilterToken.StringLiteral, value, start));
                continue;
            }

            // Number (digit or - followed by digit)
            if (char.IsDigit(ch) || (ch == '-' && position + 1 < input.Length && char.IsDigit(input[position + 1])))
            {
                var start = position;
                if (ch == '-')
                {
                    position++;
                }
                while (position < input.Length && char.IsDigit(input[position]))
                {
                    position++;
                }
                // Optional decimal part
                if (position < input.Length && input[position] == '.' && position + 1 < input.Length && char.IsDigit(input[position + 1]))
                {
                    position++; // skip dot
                    while (position < input.Length && char.IsDigit(input[position]))
                    {
                        position++;
                    }
                }
                var value = input[start..position];
                tokens.Add(new LexToken(FilterToken.Number, value, start));
                continue;
            }

            // Identifier or keyword
            if (char.IsLetter(ch) || ch == '_')
            {
                var start = position;
                position++;
                while (position < input.Length && (char.IsLetterOrDigit(input[position]) || input[position] == '_' || input[position] == '-'))
                {
                    position++;
                }
                var word = input[start..position];

                // Check for keywords (case-insensitive)
                var tokenType = word.ToUpperInvariant() switch
                {
                    "AND" => FilterToken.And,
                    "OR" => FilterToken.Or,
                    "NOT" => FilterToken.Not,
                    "EQ" => FilterToken.Eq,
                    "NE" => FilterToken.Ne,
                    "CO" => FilterToken.Co,
                    "SW" => FilterToken.Sw,
                    "EW" => FilterToken.Ew,
                    "PR" => FilterToken.Pr,
                    "GT" => FilterToken.Gt,
                    "GE" => FilterToken.Ge,
                    "LT" => FilterToken.Lt,
                    "LE" => FilterToken.Le,
                    "TRUE" => FilterToken.True,
                    "FALSE" => FilterToken.False,
                    "NULL" => FilterToken.Null,
                    _ => FilterToken.Identifier
                };

                tokens.Add(new LexToken(tokenType, word, start));
                continue;
            }

            // Unexpected character
            throw new FilterParseException(
                $"Unexpected character '{ch}' at position {position}");
        }

        return tokens;
    }
}
