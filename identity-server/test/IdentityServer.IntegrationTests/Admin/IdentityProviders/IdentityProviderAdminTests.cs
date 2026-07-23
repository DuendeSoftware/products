// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;

public sealed class IdentityProviderAdminTests : IAsyncLifetime
{
    private readonly StorageTestFixture _fixture = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<IServiceScope> _scopes = [];

    private IIdentityProviderAdmin NewAdmin()
    {
        var scope = _fixture.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();
    }

    private async Task<Guid> CreateProviderAsync(IIdentityProviderAdmin admin, string? scheme = null)
    {
        var result = await admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = scheme ?? $"provider_{Guid.NewGuid():N}",
                Type = "test"
            },
            _ct);
        result.IsSuccess.ShouldBeTrue($"CreateProvider failed: {result}");
        return result.Id;
    }

    [Fact]
    public async Task create_and_get_by_id_round_trips_all_fields()
    {
        var admin = NewAdmin();
        var scheme = $"scheme_{Guid.NewGuid():N}";
        var provider = new IdentityProviderConfiguration
        {
            Scheme = scheme,
            DisplayName = "Test Provider",
            Enabled = true,
            Type = "oidc",
            Properties = new Dictionary<string, string>
            {
                ["Authority"] = "https://idp.example.com",
                ["ClientId"] = "my-client"
            }
        };

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");
        createResult.Id.ShouldNotBe(Guid.Empty);
        createResult.Version.ShouldNotBeNull();
        createResult.Version.Value.ShouldBe(1);

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var loaded = getResult.Item;
        loaded.Scheme.ShouldBe(provider.Scheme);
        loaded.DisplayName.ShouldBe(provider.DisplayName);
        loaded.Enabled.ShouldBe(provider.Enabled);
        loaded.Type.ShouldBe(provider.Type);
        loaded.Properties.ShouldNotBeNull();
        loaded.Properties.ShouldContainKeyAndValue("Authority", "https://idp.example.com");
        loaded.Properties.ShouldContainKeyAndValue("ClientId", "my-client");
    }

    [Fact]
    public async Task create_and_get_by_scheme_round_trips()
    {
        var admin = NewAdmin();
        var scheme = $"scheme_{Guid.NewGuid():N}";
        var provider = new IdentityProviderConfiguration
        {
            Scheme = scheme,
            DisplayName = "ByScheme Test",
            Type = "test"
        };

        var createResult = await admin.CreateAsync(provider, _ct);
        createResult.IsSuccess.ShouldBeTrue($"Create failed: {createResult}");

        var getResult = await admin.GetBySchemeAsync(scheme, _ct);
        getResult.Found.ShouldBeTrue();
        getResult.Item.Scheme.ShouldBe(scheme);
        getResult.Item.DisplayName.ShouldBe("ByScheme Test");
    }

    [Fact]
    public async Task get_by_id_returns_not_found_for_nonexistent_id()
    {
        var admin = NewAdmin();

        var result = await admin.GetAsync(UuidV7.New().Value, _ct);

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task get_by_scheme_returns_not_found_for_nonexistent_scheme()
    {
        var admin = NewAdmin();

        var result = await admin.GetBySchemeAsync($"nonexistent_{Guid.NewGuid():N}", _ct);

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task create_duplicate_scheme_returns_already_exists()
    {
        var admin = NewAdmin();
        var scheme = $"scheme_{Guid.NewGuid():N}";

        var first = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = scheme, Type = "test" },
            _ct);
        first.IsSuccess.ShouldBeTrue();

        var second = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = scheme, Type = "test" },
            _ct);
        second.IsSuccess.ShouldBeFalse();
        second.Errors.ShouldNotBeNull();
        second.Errors.ShouldContain(e => e.Code == "already_exists");
    }

    [Fact]
    public async Task update_changes_fields()
    {
        var admin = NewAdmin();
        var scheme = $"scheme_{Guid.NewGuid():N}";

        var createResult = await admin.CreateAsync(
            new IdentityProviderConfiguration
            {
                Scheme = scheme,
                DisplayName = "Original",
                Enabled = true,
                Type = "test"
            },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var toUpdate = getResult.Item;
        toUpdate.DisplayName = "Updated";
        toUpdate.Enabled = false;

        var updateResult = await admin.UpdateAsync(createResult.Id, toUpdate, getResult.Version!, _ct);
        updateResult.IsSuccess.ShouldBeTrue($"Update failed: {updateResult}");

        var afterUpdate = await admin.GetAsync(createResult.Id, _ct);
        afterUpdate.Found.ShouldBeTrue();
        afterUpdate.Item.DisplayName.ShouldBe("Updated");
        afterUpdate.Item.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task update_with_wrong_version_returns_version_conflict()
    {
        var admin = NewAdmin();

        var createResult = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = $"scheme_{Guid.NewGuid():N}", Type = "test" },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeTrue();

        var wrongVersion = (DataVersion)999;
        var updateResult = await admin.UpdateAsync(createResult.Id, getResult.Item, wrongVersion, _ct);

        updateResult.IsSuccess.ShouldBeFalse();
        updateResult.Errors.ShouldNotBeNull();
        updateResult.Errors.ShouldContain(e => e.Code == "version_conflict");
    }

    [Fact]
    public async Task update_nonexistent_returns_not_found()
    {
        var admin = NewAdmin();
        var nonExistentId = Guid.CreateVersion7();

        var result = await admin.UpdateAsync(
            nonExistentId,
            new IdentityProviderConfiguration { Scheme = $"scheme_{Guid.NewGuid():N}", Type = "test" },
            (DataVersion)1,
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "not_found");
    }

    [Fact]
    public async Task delete_then_get_returns_not_found()
    {
        var admin = NewAdmin();

        var createResult = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = $"scheme_{Guid.NewGuid():N}", Type = "test" },
            _ct);
        createResult.IsSuccess.ShouldBeTrue();

        var deleteResult = await admin.DeleteAsync(createResult.Id, _ct);
        deleteResult.IsSuccess.ShouldBeTrue($"Delete failed: {deleteResult}");

        var getResult = await admin.GetAsync(createResult.Id, _ct);
        getResult.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task query_returns_all_providers()
    {
        var prefix = $"q_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        await CreateProviderAsync(admin, prefix + "a");
        await CreateProviderAsync(admin, prefix + "b");
        await CreateProviderAsync(admin, prefix + "c");

        var result = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Scheme = prefix },
                (DataRange)DataRange.FromPage(1, (DataRangeSize)100)),
            _ct);

        result.Items.ShouldNotBeNull();
        result.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task query_filters_by_scheme()
    {
        var uniquePart = $"q_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await CreateProviderAsync(admin, uniquePart + "_match1");
        await CreateProviderAsync(admin, uniquePart + "_match2");
        await CreateProviderAsync(admin, $"other_{Guid.NewGuid():N}_nomatch");

        var result = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Scheme = uniquePart }),
            _ct);

        result.Items.ShouldAllBe(p => p.Scheme.Contains(uniquePart));
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task query_filters_by_enabled()
    {
        var enabledScheme = $"q_enabled_{Guid.NewGuid():N}";
        var disabledScheme = $"q_disabled_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = enabledScheme, Type = "test", Enabled = true },
            _ct);
        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = disabledScheme, Type = "test", Enabled = false },
            _ct);

        var enabledResult = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Scheme = enabledScheme, Enabled = true }),
            _ct);

        enabledResult.Items.ShouldContain(p => p.Scheme == enabledScheme);
        enabledResult.Items.ShouldNotContain(p => p.Scheme == disabledScheme);

        var disabledResult = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Scheme = disabledScheme, Enabled = false }),
            _ct);

        disabledResult.Items.ShouldContain(p => p.Scheme == disabledScheme);
        disabledResult.Items.ShouldNotContain(p => p.Scheme == enabledScheme);
    }

    [Fact]
    public async Task query_filters_by_type()
    {
        var uniqueType = $"type_{Guid.NewGuid():N}";
        var withTypeScheme = $"q_withtype_{Guid.NewGuid():N}";
        var withoutTypeScheme = $"q_notype_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = withTypeScheme, Type = uniqueType },
            _ct);
        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = withoutTypeScheme, Type = "other" },
            _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Type = uniqueType }),
            _ct);

        result.Items.ShouldContain(p => p.Scheme == withTypeScheme);
        result.Items.ShouldNotContain(p => p.Scheme == withoutTypeScheme);
    }

    [Fact]
    public async Task query_filters_by_display_name()
    {
        var uniqueName = $"display_{Guid.NewGuid():N}";
        var matchScheme = $"q_dname_match_{Guid.NewGuid():N}";
        var noMatchScheme = $"q_dname_nomatch_{Guid.NewGuid():N}";
        var admin = NewAdmin();

        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = matchScheme, Type = "test", DisplayName = uniqueName },
            _ct);
        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = noMatchScheme, Type = "test", DisplayName = "other" },
            _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { DisplayName = uniqueName }),
            _ct);

        result.Items.ShouldContain(p => p.Scheme == matchScheme);
        result.Items.ShouldNotContain(p => p.Scheme == noMatchScheme);
    }

    [Fact]
    public async Task query_sorts_by_scheme()
    {
        var prefix = $"sort_{Guid.NewGuid():N}_";
        var admin = NewAdmin();

        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = prefix + "c", Type = "test" }, _ct);
        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = prefix + "a", Type = "test" }, _ct);
        await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = prefix + "b", Type = "test" }, _ct);

        var result = await admin.QueryAsync(
            QueryRequest.Create<IdentityProviderFilter, IdentityProviderSortField>(
                new IdentityProviderFilter { Scheme = prefix },
                SortBy.Field<IdentityProviderSortField>(IdentityProviderSortField.Scheme)),
            _ct);

        result.Items.Count.ShouldBe(3);
        var schemes = result.Items.Select(p => p.Scheme).ToList();
        schemes.ShouldBe(schemes.OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task validation_rejects_missing_scheme()
    {
        var admin = NewAdmin();

        var result = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = "", Type = "test" },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e =>
            e.Code == "required" &&
            e.PropertyNames != null &&
            e.PropertyNames.Contains("Scheme"));
    }

    [Fact]
    public async Task validation_rejects_missing_type()
    {
        var admin = NewAdmin();

        var result = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = $"scheme_{Guid.NewGuid():N}", Type = "" },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e =>
            e.Code == "required" &&
            e.PropertyNames != null &&
            e.PropertyNames.Contains("Type"));
    }

    [Fact]
    public async Task custom_validator_can_reject()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"test_{Guid.NewGuid():N}";
        services.AddStorageInternal(storage =>
            storage.AddSqliteStore(opt =>
                opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"));

        services.AddIdentityServer()
            .AddConfigurationStorage()
            .AddIdentityProviderConfigurationValidator<RejectAllIdentityProvidersValidator>();

        await using var provider = services.BuildServiceProvider();

        var schema = provider.GetRequiredService<IDatabaseSchema>();
        await schema.MigrateAsync(_ct);

        using var scope = provider.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IIdentityProviderAdmin>();

        var result = await admin.CreateAsync(
            new IdentityProviderConfiguration { Scheme = $"scheme_{Guid.NewGuid():N}", Type = "test" },
            _ct);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Code == "validation_failed");
    }

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        await _fixture.DisposeAsync();
    }

    private sealed class RejectAllIdentityProvidersValidator : IIdentityProviderConfigurationValidator
    {
        public Task ValidateAsync(IdentityProviderConfigurationValidationContext context, Ct ct)
        {
            context.SetError("Custom validator: all identity providers are rejected.");
            return Task.CompletedTask;
        }
    }
}
