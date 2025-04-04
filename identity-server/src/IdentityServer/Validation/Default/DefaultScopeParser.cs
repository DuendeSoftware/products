// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of IScopeParser.
/// </summary>
/// <param name="logger"></param>
public class DefaultScopeParser(ILogger<DefaultScopeParser> logger) : IScopeParser
{
    /// <inheritdoc/>
    public ParsedScopesResult ParseScopeValues(IEnumerable<string> scopeValues)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("DefaultScopeParser.ParseScopeValues");
        activity?.SetTag(Tracing.Properties.Scope, scopeValues.ToSpaceSeparatedString());

        var result = new ParsedScopesResult();

        if (scopeValues is null)
        {
            logger.LogError("A collection of scopes cannot be null.");
            result.Errors.Add(new ParsedScopeValidationError("null", "A collection of scopes cannot be null."));
            return result;
        }

        foreach (var scopeValue in scopeValues)
        {
            var ctx = new ParseScopeContext(scopeValue);
            ParseScopeValue(ctx);

            if (ctx.Succeeded)
            {
                var parsedScope = ctx.ParsedName != null ?
                    new ParsedScopeValue(ctx.RawValue, ctx.ParsedName, ctx.ParsedParameter) :
                    new ParsedScopeValue(ctx.RawValue);

                result.ParsedScopes.Add(parsedScope);
            }
            else if (!ctx.Ignore)
            {
                result.Errors.Add(new ParsedScopeValidationError(scopeValue, ctx.Error));
            }
            else
            {
                logger.LogDebug("Scope parsing ignoring scope {scope}", scopeValue.SanitizeLogParameter());
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a scope value.
    /// </summary>
    /// <param name="scopeContext"></param>
    /// <returns></returns>
    public virtual void ParseScopeValue(ParseScopeContext scopeContext)
    {
        // nop leaves the raw scope value as a success result.
    }

    /// <summary>
    /// Models the context for parsing a scope.
    /// </summary>
    public class ParseScopeContext
    {
        /// <summary>
        /// The original (raw) value of the scope.
        /// </summary>
        public string RawValue { get; }

        /// <summary>
        /// The parsed name of the scope. 
        /// </summary>
        public string ParsedName { get; private set; }

        /// <summary>
        /// The parsed parameter value of the scope. 
        /// </summary>
        public string ParsedParameter { get; private set; }

        /// <summary>
        /// The error encountered parsing the scope.
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// Indicates if the scope should be excluded from the parsed results.
        /// </summary>
        public bool Ignore { get; private set; }

        /// <summary>
        /// Indicates if parsing the scope was successful.
        /// </summary>
        public bool Succeeded => !Ignore && Error == null;


        /// <summary>
        /// Ctor. Indicates success, but the scope should not be included in result.
        /// </summary>
        internal ParseScopeContext(string rawScopeValue) => RawValue = rawScopeValue;

        /// <summary>
        /// Sets the parsed name and parsed parameter value for the scope.
        /// </summary>
        /// <param name="parsedName"></param>
        /// <param name="parsedParameter"></param>
        public void SetParsedValues(string parsedName, string parsedParameter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parsedName);
            ArgumentException.ThrowIfNullOrWhiteSpace(parsedParameter);

            ParsedName = parsedName;
            ParsedParameter = parsedParameter;
            Error = null;
            Ignore = false;
        }

        /// <summary>
        /// Set the error encountered parsing the scope.
        /// </summary>
        /// <param name="error"></param>
        public void SetError(string error)
        {
            ParsedName = null;
            ParsedParameter = null;
            Error = error;
            Ignore = false;
        }

        /// <summary>
        /// Sets that the scope is to be ignore/excluded from the parsed results.
        /// </summary>
        public void SetIgnore()
        {
            ParsedName = null;
            ParsedParameter = null;
            Error = null;
            Ignore = true;
        }
    }
}
