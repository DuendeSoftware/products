// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Filtering.Expressions;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;

namespace Duende.Storage.Internal.Filtering;

public sealed class FilterTranslator
{
    private readonly IScimAttributeTypeResolver _resolver;

    public FilterTranslator(IScimAttributeTypeResolver resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    public IQueryFilterExpression? Translate(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var expression = FilterExpressionParser.Parse(filter);
        return Translate(expression);
    }

    public IQueryFilterExpression Translate(FilterExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            ComparisonExpression comparison => TranslateComparison(comparison),
            LogicalExpression logical => TranslateLogical(logical),
            ComplexAttributeExpression complex => TranslateComplexAttribute(complex),
            _ => throw new NotSupportedException($"Unsupported expression type: {expression.GetType().Name}")
        };
    }

    private IQueryFilterExpression TranslateComparison(ComparisonExpression comparison)
    {
        var field = _resolver.ResolveField(comparison.AttributePath.Path);

        return comparison.Operator switch
        {
            ComparisonOperator.Equal => TranslateEqual(field, comparison.Value),
            ComparisonOperator.NotEqual => comparison.Value is null
                ? new PresentExpression(field)
                : new NotExpression(TranslateEqual(field, comparison.Value)),
            ComparisonOperator.Contains => TranslateStringOp(field, comparison.Value,
                static (sf, v) => new ContainsExpression(sf, v)),
            ComparisonOperator.StartsWith => TranslateStringOp(field, comparison.Value,
                static (sf, v) => new StartsWithExpression(sf, v)),
            ComparisonOperator.EndsWith => TranslateStringOp(field, comparison.Value,
                static (sf, v) => new EndsWithExpression(sf, v)),
            ComparisonOperator.GreaterThan => new GreaterThanExpression(field, ConvertValue(field, comparison.Value)),
            ComparisonOperator.GreaterThanOrEqual => new GreaterOrEqualExpression(field, ConvertValue(field, comparison.Value)),
            ComparisonOperator.LessThan => new LessThanExpression(field, ConvertValue(field, comparison.Value)),
            ComparisonOperator.LessThanOrEqual => new LessOrEqualExpression(field, ConvertValue(field, comparison.Value)),
            ComparisonOperator.Present => new PresentExpression(field),
            _ => throw new NotSupportedException($"Unsupported comparison operator: {comparison.Operator}")
        };
    }

    private static IQueryFilterExpression TranslateEqual(Field field, object? value)
    {
        // title eq null → not present
        if (value is null)
        {
            return new NotExpression(new PresentExpression(field));
        }

        // For string array fields, "eq" means "any element equals the value"
        if (field is StringArrayField arrayField && value is string strValue)
        {
            return new ArrayContainsExpression(arrayField, strValue.ToUpperInvariant());
        }

        // For multi-valued fields resolved from schema (not StringArrayField), equality
        // works via the standard EqualExpression with IsMultiValued producing item_index >= 0
        return new EqualExpression(field, ConvertValue(field, value));
    }

    private static IQueryFilterExpression TranslateStringOp(
        Field field,
        object? value,
        Func<StringField, string, IQueryFilterExpression> factory)
    {
        if (value is not string stringValue)
        {
            throw new InvalidOperationException(
                $"String operator requires a string value, but got {value?.GetType().Name ?? "null"}");
        }

        // For string array fields, string operations (co, sw, ew) fall back to exact element
        // matching via ArrayContainsExpression. True substring semantics on individual array
        // elements are not yet supported by the indexing model.
        if (field is StringArrayField arrayField)
        {
            return new ArrayContainsExpression(arrayField, stringValue.ToUpperInvariant());
        }

        if (field is not StringField stringField)
        {
            throw new InvalidOperationException(
                $"String operator cannot be applied to field '{field.Path}' of type {field.Type}");
        }

        return factory(stringField, stringValue.ToUpperInvariant());
    }

    private IQueryFilterExpression TranslateLogical(LogicalExpression logical) =>
        logical.Operator switch
        {
            LogicalOperator.And => new AndExpression(Translate(logical.Left), Translate(logical.Right!)),
            LogicalOperator.Or => new OrExpression(Translate(logical.Left), Translate(logical.Right!)),
            LogicalOperator.Not => new NotExpression(Translate(logical.Left)),
            _ => throw new NotSupportedException($"Unsupported logical operator: {logical.Operator}")
        };

    private ArrayFilterExpression TranslateComplexAttribute(ComplexAttributeExpression complex)
    {
        var prefixedFilter = PrefixAttributePaths(complex.Filter, complex.AttributePath.Path);
        var scopedTranslator = new FilterTranslator(new ArrayElementResolver(_resolver, complex.AttributePath.Path));
        var innerFilter = scopedTranslator.Translate(prefixedFilter);
        return new ArrayFilterExpression(complex.AttributePath.Path, innerFilter);
    }

    private static FilterExpression PrefixAttributePaths(FilterExpression expression, string prefix) =>
        expression switch
        {
            ComparisonExpression comparison => new ComparisonExpression(
                new AttributePathExpression($"{prefix}.{comparison.AttributePath.Path}"),
                comparison.Operator,
                comparison.Value),
            LogicalExpression logical => logical.Operator == LogicalOperator.Not
                ? new LogicalExpression(LogicalOperator.Not, PrefixAttributePaths(logical.Left, prefix))
                : new LogicalExpression(logical.Operator,
                    PrefixAttributePaths(logical.Left, prefix),
                    PrefixAttributePaths(logical.Right!, prefix)),
            _ => expression
        };

    /// <summary>
    /// Wraps a resolver to strip the array prefix from resolved field paths.
    /// Inside an array filter, fields are per-element and the SQL builder
    /// re-adds the array prefix when building the query.
    /// </summary>
    private sealed class ArrayElementResolver(IScimAttributeTypeResolver inner, string arrayPrefix) : IScimAttributeTypeResolver
    {
        public Field ResolveField(string attributePath)
        {
            var field = inner.ResolveField(attributePath);
            var strippedPath = StripPrefix(field.Path);
            return field switch
            {
                StringField => new StringField(strippedPath),
                NumberField => new NumberField(strippedPath),
                BooleanField => new BooleanField(strippedPath),
                DateTimeField => new DateTimeField(strippedPath),
                GuidField => new GuidField(strippedPath),
                _ => field
            };
        }

        private string StripPrefix(string path)
        {
            var prefix = arrayPrefix.ToUpperInvariant() + ".";
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path[prefix.Length..]
                : path;
        }
    }

    private static object ConvertValue(Field field, object? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot convert null value for comparison");
        }

        return field.Type switch
        {
            FieldType.String => (value is string s
                ? s
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!).ToUpperInvariant(),

            FieldType.Number => value switch
            {
                decimal d => d,
                int i => (decimal)i,
                long l => (decimal)l,
                double d => decimal.Parse(d.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
                float f => decimal.Parse(f.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture),
                string s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
                _ => Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)
            },

            FieldType.Boolean => value switch
            {
                bool b => b,
                string s => bool.Parse(s),
                _ => Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)
            },

            FieldType.DateTime => value switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
                // Use AssumeUniversal so date-only strings (e.g. "2024-01-01") are interpreted as UTC,
                // matching how DateOnly values are stored as DateTimeOffset at midnight UTC.
                string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal),
                _ => throw new InvalidOperationException($"Cannot convert {value.GetType().Name} to DateTimeOffset")
            },

            _ => value
        };
    }
}
