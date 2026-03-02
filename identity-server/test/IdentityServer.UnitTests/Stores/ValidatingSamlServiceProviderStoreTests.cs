// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Runtime.CompilerServices;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Stores;

public class ValidatingSamlServiceProviderStoreTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private readonly TestEventService _events = new();
    private readonly NullLogger<ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>> _logger = new();

    [Fact]
    public async Task FindByEntityIdAsync_WhenSpIsValid_ShouldReturnSp()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp1.example.com" };
        var innerStore = StubSamlServiceProviderStore.WithSp(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldNotBeNull();
        result.EntityId.ShouldBe(sp.EntityId);
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSpIsInvalid_ShouldReturnNull()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp1.example.com" };
        var innerStore = StubSamlServiceProviderStore.WithSp(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync(sp.EntityId, _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSpIsInvalid_ShouldRaiseEvent()
    {
        var sp = new SamlServiceProvider { EntityId = "https://sp1.example.com" };
        var innerStore = StubSamlServiceProviderStore.WithSp(sp);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        await store.FindByEntityIdAsync(sp.EntityId, _ct);

        _events.AssertEventWasRaised<InvalidSamlServiceProviderConfigurationEvent>();
    }

    [Fact]
    public async Task FindByEntityIdAsync_WhenSpNotFound_ShouldReturnNull()
    {
        var innerStore = StubSamlServiceProviderStore.Empty();
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = await store.FindByEntityIdAsync("https://notfound.example.com", _ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenAllSpsAreValid_ShouldReturnAll()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://sp1.example.com" },
            new() { EntityId = "https://sp2.example.com" },
            new() { EntityId = "https://sp3.example.com" }
        };
        var innerStore = StubSamlServiceProviderStore.WithSps(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.Count.ShouldBe(3);
        result.ShouldContain(s => s.EntityId == "https://sp1.example.com");
        result.ShouldContain(s => s.EntityId == "https://sp2.example.com");
        result.ShouldContain(s => s.EntityId == "https://sp3.example.com");
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenSomeSpsAreInvalid_ShouldReturnOnlyValid()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://valid1.example.com" },
            new() { EntityId = "https://invalid1.example.com" },
            new() { EntityId = "https://valid2.example.com" }
        };
        var innerStore = StubSamlServiceProviderStore.WithSps(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(
            validationFunc: sp => !sp.EntityId.Contains("invalid"),
            errorMessage: "SP is invalid"
        );
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.Count.ShouldBe(2);
        result.ShouldContain(s => s.EntityId == "https://valid1.example.com");
        result.ShouldContain(s => s.EntityId == "https://valid2.example.com");
        result.ShouldNotContain(s => s.EntityId == "https://invalid1.example.com");
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenSpIsInvalid_ShouldRaiseEvent()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://invalid.example.com" }
        };
        var innerStore = StubSamlServiceProviderStore.WithSps(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldBeEmpty();
        _events.AssertEventWasRaised<InvalidSamlServiceProviderConfigurationEvent>();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenNoSps_ShouldReturnEmpty()
    {
        var innerStore = StubSamlServiceProviderStore.Empty();
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: true);
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, _events, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllSamlServiceProvidersAsync_WhenAllInvalid_ShouldRaiseMultipleEvents()
    {
        var sps = new List<SamlServiceProvider>
        {
            new() { EntityId = "https://invalid1.example.com" },
            new() { EntityId = "https://invalid2.example.com" }
        };
        var innerStore = StubSamlServiceProviderStore.WithSps(sps);
        var validator = new StubSamlServiceProviderConfigurationValidator(isValid: false, errorMessage: "All invalid");
        var eventService = new StubEventService();
        var store = new ValidatingSamlServiceProviderStore<StubSamlServiceProviderStore>(innerStore, validator, eventService, _logger);

        var result = new List<SamlServiceProvider>();
        await foreach (var sp in store.GetAllSamlServiceProvidersAsync(_ct))
        {
            result.Add(sp);
        }

        result.ShouldBeEmpty();
        eventService.RaisedEventCount.ShouldBe(2);
    }

    private class StubSamlServiceProviderStore : ISamlServiceProviderStore
    {
        private readonly SamlServiceProvider? _sp;
        private readonly IEnumerable<SamlServiceProvider> _sps;

        private StubSamlServiceProviderStore(SamlServiceProvider? sp, IEnumerable<SamlServiceProvider> sps)
        {
            _sp = sp;
            _sps = sps;
        }

        public static StubSamlServiceProviderStore Empty() => new(null, []);
        public static StubSamlServiceProviderStore WithSp(SamlServiceProvider sp) => new(sp, [sp]);
        public static StubSamlServiceProviderStore WithSps(IEnumerable<SamlServiceProvider> sps) => new(sps.FirstOrDefault(), sps);

        public Task<SamlServiceProvider?> FindByEntityIdAsync(string entityId, Ct _) => Task.FromResult(_sp);

        public async IAsyncEnumerable<SamlServiceProvider> GetAllSamlServiceProvidersAsync([EnumeratorCancellation] Ct _)
        {
            foreach (var sp in _sps)
            {
                yield return sp;
            }
        }
    }

    private class StubSamlServiceProviderConfigurationValidator : ISamlServiceProviderConfigurationValidator
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
            _isValid = true;
        }

        public Task ValidateAsync(SamlServiceProviderConfigurationValidationContext context)
        {
            var isValid = _validationFunc != null ? _validationFunc(context.ServiceProvider) : _isValid;

            if (!isValid)
            {
                context.SetError(_errorMessage ?? "Validation failed");
            }

            return Task.CompletedTask;
        }
    }

    private class StubEventService : IEventService
    {
        public int RaisedEventCount { get; private set; }

        public bool CanRaiseEventType(EventTypes evtType) => true;

        public Task RaiseAsync(Event evt, Ct _)
        {
            RaisedEventCount++;
            return Task.CompletedTask;
        }
    }
}
