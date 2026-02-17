// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;

namespace UnitTests.Stores;

public class ValidatingClientStoreTests
{
    private readonly TestEventService _events = new();
    private readonly NullLogger<ValidatingClientStore<StubClientStore>> _logger = new();

    [Fact]
    public async Task GetAllClientsAsync_WhenAllClientsAreValid_ShouldReturnAllClients()
    {
        var clients = new List<Client>
        {
            new() { ClientId = "client1" },
            new() { ClientId = "client2" },
            new() { ClientId = "client3" }
        };
        var innerStore = StubClientStore.WithClients(clients);
        var validator = new StubClientConfigurationValidator(isValid: true);
        var store = new ValidatingClientStore<StubClientStore>(innerStore, validator, _events, _logger);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.Count.ShouldBe(3);
        result.ShouldContain(c => c.ClientId == "client1");
        result.ShouldContain(c => c.ClientId == "client2");
        result.ShouldContain(c => c.ClientId == "client3");
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenSomeClientsAreInvalid_ShouldReturnOnlyValidClients()
    {
        var clients = new List<Client>
        {
            new() { ClientId = "valid1" },
            new() { ClientId = "invalid1" },
            new() { ClientId = "valid2" }
        };
        var innerStore = StubClientStore.WithClients(clients);
        // Validator that marks clients with "invalid" in the name as invalid
        var validator = new StubClientConfigurationValidator(
            validationFunc: client => !client.ClientId.Contains("invalid"),
            errorMessage: "Client is invalid"
        );
        var store = new ValidatingClientStore<StubClientStore>(innerStore, validator, _events, _logger);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.ClientId == "valid1");
        result.ShouldContain(c => c.ClientId == "valid2");
        result.ShouldNotContain(c => c.ClientId == "invalid1");
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenClientIsInvalid_ShouldRaiseEvent()
    {
        var clients = new List<Client>
        {
            new() { ClientId = "invalid_client" }
        };
        var innerStore = StubClientStore.WithClients(clients);
        var validator = new StubClientConfigurationValidator(isValid: false, errorMessage: "Invalid configuration");
        var store = new ValidatingClientStore<StubClientStore>(innerStore, validator, _events, _logger);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.ShouldBeEmpty();
        _events.AssertEventWasRaised<InvalidClientConfigurationEvent>();
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenNoClients_ShouldReturnEmpty()
    {
        var innerStore = StubClientStore.Empty();
        var validator = new StubClientConfigurationValidator(isValid: true);
        var store = new ValidatingClientStore<StubClientStore>(innerStore, validator, _events, _logger);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllClientsAsync_WhenAllClientsAreInvalid_ShouldReturnEmpty()
    {
        var clients = new List<Client>
        {
            new() { ClientId = "invalid1" },
            new() { ClientId = "invalid2" }
        };
        var innerStore = StubClientStore.WithClients(clients);
        var validator = new StubClientConfigurationValidator(isValid: false, errorMessage: "All invalid");
        // Use a stub event service that allows multiple events of the same type
        var eventService = new StubEventService();
        var store = new ValidatingClientStore<StubClientStore>(innerStore, validator, eventService, _logger);

        var result = new List<Client>();
        await foreach (var client in store.GetAllClientsAsync())
        {
            result.Add(client);
        }

        result.ShouldBeEmpty();
        eventService.RaisedEventCount.ShouldBe(2);
    }


    private class StubClientStore : IClientStore
    {
        private readonly Client? _client;
        private readonly IEnumerable<Client> _clients;

        private StubClientStore(Client? client, IEnumerable<Client> clients)
        {
            _client = client;
            _clients = clients;
        }

        public static StubClientStore Empty() => new(null, []);

        public static StubClientStore WithClient(Client client) => new(client, [client]);

        public static StubClientStore WithClients(IEnumerable<Client> clients) => new(clients.FirstOrDefault(), clients);

        public Task<Client?> FindClientByIdAsync(string clientId) => Task.FromResult(_client);

        public async IAsyncEnumerable<Client> GetAllClientsAsync()
        {
            foreach (var client in _clients)
            {
                yield return client;
            }
        }
    }

    private class StubClientConfigurationValidator : IClientConfigurationValidator
    {
        private readonly bool _isValid;
        private readonly string? _errorMessage;
        private readonly Func<Client, bool>? _validationFunc;

        public StubClientConfigurationValidator(bool isValid, string? errorMessage = null)
        {
            _isValid = isValid;
            _errorMessage = errorMessage;
        }

        public StubClientConfigurationValidator(Func<Client, bool> validationFunc, string? errorMessage = null)
        {
            _validationFunc = validationFunc;
            _errorMessage = errorMessage;
        }

        public Task ValidateAsync(ClientConfigurationValidationContext context)
        {
            var isValid = _validationFunc != null ? _validationFunc(context.Client) : _isValid;

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

        public Task RaiseAsync(Event evt)
        {
            RaisedEventCount++;
            return Task.CompletedTask;
        }
    }
}
