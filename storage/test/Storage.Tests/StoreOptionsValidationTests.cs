// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.MsSql;
using Duende.Storage.PostgreSql;

namespace Duende.Storage;

public sealed class StoreOptionsValidationTests
{
    [Fact]
    public void mssql_schema_name_at_max_length_passes()
    {
        var options = new MsSqlStoreOptions { SchemaName = new string('a', 88) };
        var results = ValidateOptions(options);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void mssql_schema_name_exceeding_max_length_fails()
    {
        var options = new MsSqlStoreOptions { SchemaName = new string('a', 89) };
        var results = ValidateOptions(options);
        results.ShouldContain(r => r.MemberNames.Contains("SchemaName"));
    }

    [Fact]
    public void postgresql_schema_name_at_max_length_passes()
    {
        var options = new PostgreSqlStoreOptions { SchemaName = new string('a', 63) };
        var results = ValidateOptions(options);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void postgresql_schema_name_exceeding_max_length_fails()
    {
        var options = new PostgreSqlStoreOptions { SchemaName = new string('a', 64) };
        var results = ValidateOptions(options);
        results.ShouldContain(r => r.MemberNames.Contains("SchemaName"));
    }

    private static List<ValidationResult> ValidateOptions<T>(T options) where T : notnull
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        _ = Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
