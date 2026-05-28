// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Stores;

public class ValidatingSamlServiceProviderStoreTests
{
    private readonly TestEventService _events = new();
    private readonly NullLogger<ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>> _logger = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static SamlServiceProvider ValidSp(string entityId = "https://sp.example.com") =>
        new()
        {
            EntityId = entityId,
            AssertionConsumerServiceUrls = [new IndexedEndpoint { Location = "https://sp.example.com/acs", Index = 0 }],
            AllowedScopes = ["openid"]
        };

    [Fact]
    public async Task FindByEntityIdAsync_WhenValidationPasses_ShouldReturnServiceProvider()
    {
        var sp = ValidSp();
        var innerStore = StubSamlServiceProviderStore.WithServiceProvider(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe(sp.EntityId);
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenValidationFails_ShouldReturnNull()
    {
        var sp = ValidSp();
        var innerStore = StubSamlServiceProviderStore.WithServiceProvider(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenValidationFails_ShouldRaiseEvent()
    {
        var sp = ValidSp();
        var innerStore = StubSamlServiceProviderStore.WithServiceProvider(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        await store.FindByEntityIdAsync(sp.EntityId, _ct);

        _events.AssertEventWasRaised<InvalidSamlServiceProviderConfigurationEvent>();
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenServiceProviderNotFound_ShouldReturnNull()
    {
        var innerStore = StubSamlServiceProviderStore.Empty();
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync("https://unknown.example.com", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenAllValid_ShouldReturnAll()
    {
        var sps = new List<SamlServiceProvider>
        {
            ValidSp("https://sp1.example.com"),
            ValidSp("https://sp2.example.com"),
            ValidSp("https://sp3.example.com")
        };
        var innerStore = StubSamlServiceProviderStore.WithServiceProviders(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenSomeInvalid_ShouldFilterOutInvalid()
    {
        var sps = new List<SamlServiceProvider>
        {
            ValidSp("https://valid1.example.com"),
            ValidSp("https://invalid1.example.com"),
            ValidSp("https://valid2.example.com")
        };
        var innerStore = StubSamlServiceProviderStore.WithServiceProviders(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(
            validationFunc: sp => !sp.EntityId.Contains("invalid"),
            errorMessage: "Service provider is invalid"
        );
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.Count.ShouldBe(2);
        result.ShouldContain(sp => sp.EntityId == "https://valid1.example.com");
        result.ShouldContain(sp => sp.EntityId == "https://valid2.example.com");
        result.ShouldNotContain(sp => sp.EntityId == "https://invalid1.example.com");
    }

    private sealed class StubSamlServiceProviderStore : ISamlServiceProviderStore
    {
        private readonly SamlServiceProvider? _serviceProvider;
        private readonly IEnumerable<SamlServiceProvider> _serviceProviders;

        private StubSamlServiceProviderStore(SamlServiceProvider? serviceProvider, IEnumerable<SamlServiceProvider> serviceProviders)
        {
            _serviceProvider = serviceProvider;
            _serviceProviders = serviceProviders;
        }

        public static StubSamlServiceProviderStore Empty() => new(null, []);

        public static StubSamlServiceProviderStore WithServiceProvider(SamlServiceProvider sp) => new(sp, [sp]);

        public static StubSamlServiceProviderStore WithServiceProviders(IEnumerable<SamlServiceProvider> sps) =>
            new(sps.FirstOrDefault(), sps);

        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct _) =>
            Task.FromResult(_serviceProvider);

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct _)
        {
            foreach (var sp in _serviceProviders)
            {
                yield return sp;
            }
        }
    }

    private sealed class StubSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
    {
        private readonly bool _isValid;
        private readonly string? _errorMessage;
        private readonly Func<SamlServiceProvider, bool>? _validationFunc;

        public StubSamlServiceProviderConfigurationValidator(bool isValid, string? errorMessage = null)
        {
            _isValid = isValid;
            _errorMessage = errorMessage;
        }

        public StubSamlServiceProviderConfigurationValidator(Func<SamlServiceProvider, bool> validationFunc, string? errorMessage = null)
        {
            _validationFunc = validationFunc;
            _errorMessage = errorMessage;
        }

        public Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context, Ct _)
        {
            var isValid = _validationFunc != null ? _validationFunc(context.ServiceProvider) : _isValid;

            if (!isValid)
            {
                context.SetError(_errorMessage ?? "Validation failed");
            }

            return Task.CompletedTask;
        }
    }
}
