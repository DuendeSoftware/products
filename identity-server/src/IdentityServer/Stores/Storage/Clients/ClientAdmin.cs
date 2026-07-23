// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Cryptography;
using System.Text;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using SecretHashAlgorithm = Duende.IdentityServer.Admin.SecretHashAlgorithm;

namespace Duende.IdentityServer.Stores.Storage.Clients;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ClientAdmin(
    ClientRepository repository,
    IClientConfigurationValidator extensionPointValidator,
    ISchemaStore schemaStore) : IClientAdmin
{
    private readonly IConfigurationValidator<ClientConfiguration>[] validators =
    [
        new ClientStructureValidator(),
        new ClientExtensionPointValidator(extensionPointValidator),
        new ClientExtensionPropertyValidator(schemaStore),
    ];

    public async Task<SaveResult<Guid>> CreateAsync(CreateClient client, Ct ct)
    {
        var secretError = ValidateCreateSecrets(client);
        if (secretError is not null)
        {
            return secretError;
        }

        var configuration = MapToClientConfiguration(client);
        var validationErrors = await RunValidationPipelineAsync(configuration, ct);
        if (validationErrors is not null)
        {
            return validationErrors;
        }

        var id = UuidV7.New();
        var dso = MapToDso(id.Value, client);

        var result = await repository.CreateAsync(id, dso, ct);

        return result switch
        {
            CreateResult.Success => SaveResult.Success(id.Value, (DataVersion)1),
            CreateResult.AlreadyExists or CreateResult.KeyConflict =>
                AdminError.AlreadyExists("client", client.ClientId),
            _ => throw new InvalidOperationException($"Unexpected CreateResult: {result}")
        };
    }

    public async Task<GetResult<ClientConfiguration>> GetAsync(Guid id, Ct ct)
    {
        var result = await repository.TryReadByIdAsync(id, ct);
        if (result is null)
        {
            return GetResult.NotFound<ClientConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<GetResult<ClientConfiguration>> GetByClientIdAsync(string clientId, Ct ct)
    {
        var result = await repository.TryReadByClientIdAsync(clientId, ct);
        if (result is null)
        {
            return GetResult.NotFound<ClientConfiguration>();
        }

        var (dso, version) = result.Value;
        return GetResult.Found(MapToConfiguration(dso), (DataVersion)version);
    }

    public async Task<SaveResult<Guid>> UpdateAsync(Guid id, UpdateClient client, DataVersion expectedVersion, Ct ct)
    {
        // Load existing DSO to preserve secrets
        var existing = await repository.TryReadByIdAsync(id, ct);
        if (existing is null)
        {
            return AdminError.NotFound("client", id.ToString());
        }

        var (existingDso, _) = existing.Value;

        var configuration = MapToClientConfiguration(client, existingDso.ClientSecrets);
        var validationErrors = await RunValidationPipelineAsync(configuration, ct);
        if (validationErrors is not null)
        {
            return validationErrors;
        }

        var dso = MapToDso(id, client, existingDso.ClientSecrets);

        var result = await repository.UpdateAsync(UuidV7.From(id), dso, expectedVersion.Value, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(id, (DataVersion)(expectedVersion.Value + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("client", id.ToString()),
            UpdateResult.KeyConflict => AdminError.AlreadyExists("client", client.ClientId),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<Guid>> DeleteAsync(Guid id, Ct ct)
    {
        var result = await repository.DeleteAsync(id, ct);

        return result switch
        {
            DeleteResult.Success => SaveResult.Success(id, (DataVersion)0),
            _ => throw new InvalidOperationException($"Unexpected DeleteResult: {result}")
        };
    }

    public async Task<Duende.Storage.Querying.QueryResult<ClientListItem>> QueryAsync(QueryRequest<ClientFilter, ClientSortField> request, Ct ct)
    {
        var result = await repository.QueryAsync(request, ct);
        return result.ConvertTo(MapToListItem);
    }

    // === Secret Management ===

    public async Task<SaveResult<Guid>> CreateSecretAsync(Guid clientId, CreateClientSecret secret, Ct ct)
    {
        if (string.IsNullOrWhiteSpace(secret.PlaintextValue))
        {
            return AdminError.Required("PlaintextValue");
        }

        if (secret.Type is not null && string.IsNullOrWhiteSpace(secret.Type))
        {
            return AdminError.InvalidValue("Type", "Secret type must not be empty or whitespace.");
        }

        var existing = await repository.TryReadByIdAsync(clientId, ct);
        if (existing is null)
        {
            return AdminError.NotFound("client", clientId.ToString());
        }

        var (dso, version) = existing.Value;

        var algorithm = secret.HashAlgorithm ?? SecretHashAlgorithm.Sha256;
        var hashedValue = HashSecret(secret.PlaintextValue, algorithm);
        var algorithmName = algorithm == SecretHashAlgorithm.Sha512 ? "SHA512" : "SHA256";

        var secretId = UuidV7.New().Value;
        var newSecret = new ClientDso.SecretDso(
            Id: secretId,
            Value: hashedValue,
            Description: secret.Description,
            Expiration: secret.Expiration,
            Type: secret.Type ?? IdentityServerConstants.SecretTypes.SharedSecret,
            HashAlgorithm: algorithmName);

        var updatedSecrets = dso.ClientSecrets.Append(newSecret).ToList();
        var updatedDso = dso with { ClientSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(UuidV7.From(clientId), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(secretId, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("client", clientId.ToString()),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    public async Task<SaveResult<Guid>> DeleteSecretAsync(Guid clientId, Guid secretId, Ct ct)
    {
        var existing = await repository.TryReadByIdAsync(clientId, ct);
        if (existing is null)
        {
            return AdminError.NotFound("client", clientId.ToString());
        }

        var (dso, version) = existing.Value;

        var secretToDelete = dso.ClientSecrets.FirstOrDefault(s => s.Id == secretId);
        if (secretToDelete is null)
        {
            return AdminError.NotFound("secret", secretId.ToString());
        }

        var updatedSecrets = dso.ClientSecrets.Where(s => s.Id != secretId).ToList();
        var updatedDso = dso with { ClientSecrets = updatedSecrets };

        var result = await repository.UpdateAsync(UuidV7.From(clientId), updatedDso, version, ct);

        return result switch
        {
            UpdateResult.Success => SaveResult.Success(secretId, (DataVersion)(version + 1)),
            UpdateResult.UnexpectedVersion => AdminError.VersionConflict(),
            UpdateResult.DoesNotExist => AdminError.NotFound("client", clientId.ToString()),
            _ => throw new InvalidOperationException($"Unexpected UpdateResult: {result}")
        };
    }

    // === Structural Validation ===

    private static AdminError? ValidateCreateSecrets(CreateClient client)
    {
        if (client.ClientSecrets is not null)
        {
            foreach (var secret in client.ClientSecrets)
            {
                if (string.IsNullOrWhiteSpace(secret.PlaintextValue))
                {
                    return AdminError.Required("ClientSecrets.PlaintextValue");
                }

                if (secret.Type is not null && string.IsNullOrWhiteSpace(secret.Type))
                {
                    return AdminError.InvalidValue("ClientSecrets.Type", "Secret type must not be empty or whitespace.");
                }
            }
        }

        return null;
    }

    // === Validation Pipeline ===

    private async Task<AdminError?> RunValidationPipelineAsync(ClientConfiguration configuration, Ct ct)
    {
        foreach (var validator in validators)
        {
            var errors = await validator.ValidateAsync(configuration, ct);
            if (errors is { Count: > 0 })
            {
                return errors[0];
            }
        }

        return null;
    }

    private static ClientConfiguration MapToClientConfiguration(UpdateClient client, IReadOnlyList<ClientDso.SecretDso> existingSecrets) =>
        new()
        {
            ClientId = client.ClientId,
            Enabled = client.Enabled,
            ClientName = client.ClientName,
            Description = client.Description,
            ClientUri = client.ClientUri,
            LogoUri = client.LogoUri,
            RequireClientSecret = client.RequireClientSecret,
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RequireRequestObject = client.RequireRequestObject,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = client.DPoPValidationMode,
            DPoPClockSkew = client.DPoPClockSkew,
            RequireConsent = client.RequireConsent,
            AllowRememberConsent = client.AllowRememberConsent,
            ConsentLifetime = client.ConsentLifetime,
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = client.AllowOfflineAccess,
            AccessTokenType = client.AccessTokenType,
            IncludeJwtId = client.IncludeJwtId,
            IdentityTokenLifetime = client.IdentityTokenLifetime,
            AccessTokenLifetime = client.AccessTokenLifetime,
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = client.RefreshTokenUsage,
            RefreshTokenExpiration = client.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,
            AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = client.AlwaysSendClientClaims,
            ClientClaimsPrefix = client.ClientClaimsPrefix,
            PairWiseSubjectSalt = client.PairWiseSubjectSalt,
            UserSsoLifetime = client.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,
            EnableLocalLogin = client.EnableLocalLogin,
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
            InitiateLoginUri = client.InitiateLoginUri,
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,
            UserCodeType = client.UserCodeType,
            DeviceCodeLifetime = client.DeviceCodeLifetime,
            CibaLifetime = client.CibaLifetime,
            PollingInterval = client.PollingInterval,
            AllowedGrantTypes = client.AllowedGrantTypes?.AsReadOnly(),
            AllowedScopes = client.AllowedScopes?.AsReadOnly(),
            RedirectUris = client.RedirectUris?.AsReadOnly(),
            PostLogoutRedirectUris = client.PostLogoutRedirectUris?.AsReadOnly(),
            AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms?.AsReadOnly(),
            IdentityProviderRestrictions = client.IdentityProviderRestrictions?.AsReadOnly(),
            AllowedCorsOrigins = client.AllowedCorsOrigins?.AsReadOnly(),
            Claims = client.Claims?.Select(c => new ClientClaimConfiguration
            {
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType
            }).ToList().AsReadOnly(),
            ClientSecrets = existingSecrets.Select(s => new ClientSecretConfiguration
            {
                Id = s.Id,
                Type = s.Type,
                Description = s.Description,
                Expiration = s.Expiration
            }).ToArray(),
            ExtendedProperties = client.ExtendedProperties.ToList().AsReadOnly()
        };

    private static ClientConfiguration MapToClientConfiguration(CreateClient client) =>
        new()
        {
            ClientId = client.ClientId,
            Enabled = client.Enabled,
            ClientName = client.ClientName,
            Description = client.Description,
            ClientUri = client.ClientUri,
            LogoUri = client.LogoUri,
            RequireClientSecret = client.RequireClientSecret,
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RequireRequestObject = client.RequireRequestObject,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = client.DPoPValidationMode,
            DPoPClockSkew = client.DPoPClockSkew,
            RequireConsent = client.RequireConsent,
            AllowRememberConsent = client.AllowRememberConsent,
            ConsentLifetime = client.ConsentLifetime,
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = client.AllowOfflineAccess,
            AccessTokenType = client.AccessTokenType,
            IncludeJwtId = client.IncludeJwtId,
            IdentityTokenLifetime = client.IdentityTokenLifetime,
            AccessTokenLifetime = client.AccessTokenLifetime,
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = client.RefreshTokenUsage,
            RefreshTokenExpiration = client.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,
            AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = client.AlwaysSendClientClaims,
            ClientClaimsPrefix = client.ClientClaimsPrefix,
            PairWiseSubjectSalt = client.PairWiseSubjectSalt,
            UserSsoLifetime = client.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,
            EnableLocalLogin = client.EnableLocalLogin,
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
            InitiateLoginUri = client.InitiateLoginUri,
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,
            UserCodeType = client.UserCodeType,
            DeviceCodeLifetime = client.DeviceCodeLifetime,
            CibaLifetime = client.CibaLifetime,
            PollingInterval = client.PollingInterval,
            AllowedGrantTypes = client.AllowedGrantTypes?.AsReadOnly(),
            AllowedScopes = client.AllowedScopes?.AsReadOnly(),
            RedirectUris = client.RedirectUris?.AsReadOnly(),
            PostLogoutRedirectUris = client.PostLogoutRedirectUris?.AsReadOnly(),
            AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms?.AsReadOnly(),
            IdentityProviderRestrictions = client.IdentityProviderRestrictions?.AsReadOnly(),
            AllowedCorsOrigins = client.AllowedCorsOrigins?.AsReadOnly(),
            Claims = client.Claims?.Select(c => new ClientClaimConfiguration
            {
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType
            }).ToList().AsReadOnly(),
            ClientSecrets = client.ClientSecrets?.Select(x => new ClientSecretConfiguration()
            {
                Id = Guid.NewGuid(),
                Type = x.Type ?? IdentityServerConstants.SecretTypes.SharedSecret,
                Description = x.Description,
                Expiration = x.Expiration
            }).ToArray() ?? [],

            ExtendedProperties = client.ExtendedProperties.ToList().AsReadOnly()
        };

    // === Mapping ===

    internal static Secret MapToIsSecret(ClientDso.SecretDso secret) => new()
    {
        Value = secret.Value,
        Description = secret.Description,
        Expiration = secret.Expiration,
        Type = secret.Type
    };

    internal static IReadOnlyList<Secret> MapToIsSecrets(IReadOnlyList<ClientDso.SecretDso> secrets) =>
        secrets.Select(MapToIsSecret).ToList().AsReadOnly();

    private static ClientDso.V1 MapToDso(Guid id, CreateClient client)
    {
        var secrets = client.ClientSecrets?.Select(MapToSecretDso).ToList()
                     ?? [];

        return new ClientDso.V1
        {
            // Identity
            Id = id,
            ClientId = client.ClientId,
            Enabled = client.Enabled,
            ProtocolType = IdentityServerConstants.ProtocolTypes.OpenIdConnect,

            // Display
            ClientName = client.ClientName,
            Description = client.Description,
            ClientUri = client.ClientUri,
            LogoUri = client.LogoUri,

            // Authentication
            RequireClientSecret = client.RequireClientSecret,
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RequireRequestObject = client.RequireRequestObject,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = (int)client.DPoPValidationMode,
            DPoPClockSkewTicks = client.DPoPClockSkew.Ticks,

            // Consent
            RequireConsent = client.RequireConsent,
            AllowRememberConsent = client.AllowRememberConsent,
            ConsentLifetime = client.ConsentLifetime,

            // Tokens
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = client.AllowOfflineAccess,
            AccessTokenType = (int)client.AccessTokenType,
            IncludeJwtId = client.IncludeJwtId,
            IdentityTokenLifetime = client.IdentityTokenLifetime,
            AccessTokenLifetime = client.AccessTokenLifetime,
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,

            // Refresh
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = (int)client.RefreshTokenUsage,
            RefreshTokenExpiration = (int)client.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,

            // Claims
            AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = client.AlwaysSendClientClaims,
            ClientClaimsPrefix = client.ClientClaimsPrefix,
            PairWiseSubjectSalt = client.PairWiseSubjectSalt,

            // Session
            UserSsoLifetime = client.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,

            // Login/Logout
            EnableLocalLogin = client.EnableLocalLogin,
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
            InitiateLoginUri = client.InitiateLoginUri,

            // PAR
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,

            // Device/CIBA
            UserCodeType = client.UserCodeType,
            DeviceCodeLifetime = client.DeviceCodeLifetime,
            CibaLifetime = client.CibaLifetime,
            PollingInterval = client.PollingInterval,

            // Collections
            AllowedGrantTypes = client.AllowedGrantTypes?.AsReadOnly() ?? [],
            AllowedScopes = client.AllowedScopes?.AsReadOnly() ?? [],
            RedirectUris = client.RedirectUris?.AsReadOnly() ?? [],
            PostLogoutRedirectUris = client.PostLogoutRedirectUris?.AsReadOnly() ?? [],
            AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms?.AsReadOnly() ?? [],
            IdentityProviderRestrictions = client.IdentityProviderRestrictions?.AsReadOnly() ?? [],
            AllowedCorsOrigins = client.AllowedCorsOrigins?.AsReadOnly() ?? [],

            ClientSecrets = secrets,

            Claims = client.Claims?
                .Select(c => new ClientDso.ClaimDso(c.Type, c.Value, c.ValueType))
                .ToList() ?? [],

            ExtendedAttributeValues = EavPropertyMapper.SerializeFromCollection(client.ExtendedProperties)
        };
    }

    private static ClientDso.V1 MapToDso(
        Guid id,
        UpdateClient client,
        IReadOnlyList<ClientDso.SecretDso> existingSecrets) =>
        new()
        {
            // Identity
            Id = id,
            ClientId = client.ClientId,
            Enabled = client.Enabled,
            ProtocolType = IdentityServerConstants.ProtocolTypes.OpenIdConnect,

            // Display
            ClientName = client.ClientName,
            Description = client.Description,
            ClientUri = client.ClientUri,
            LogoUri = client.LogoUri,

            // Authentication
            RequireClientSecret = client.RequireClientSecret,
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RequireRequestObject = client.RequireRequestObject,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = (int)client.DPoPValidationMode,
            DPoPClockSkewTicks = client.DPoPClockSkew.Ticks,

            // Consent
            RequireConsent = client.RequireConsent,
            AllowRememberConsent = client.AllowRememberConsent,
            ConsentLifetime = client.ConsentLifetime,

            // Tokens
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = client.AllowOfflineAccess,
            AccessTokenType = (int)client.AccessTokenType,
            IncludeJwtId = client.IncludeJwtId,
            IdentityTokenLifetime = client.IdentityTokenLifetime,
            AccessTokenLifetime = client.AccessTokenLifetime,
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,

            // Refresh
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = (int)client.RefreshTokenUsage,
            RefreshTokenExpiration = (int)client.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh,

            // Claims
            AlwaysIncludeUserClaimsInIdToken = client.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = client.AlwaysSendClientClaims,
            ClientClaimsPrefix = client.ClientClaimsPrefix,
            PairWiseSubjectSalt = client.PairWiseSubjectSalt,

            // Session
            UserSsoLifetime = client.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession,

            // Login/Logout
            EnableLocalLogin = client.EnableLocalLogin,
            FrontChannelLogoutUri = client.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = client.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = client.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = client.BackChannelLogoutSessionRequired,
            InitiateLoginUri = client.InitiateLoginUri,

            // PAR
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            PushedAuthorizationLifetime = client.PushedAuthorizationLifetime,

            // Device/CIBA
            UserCodeType = client.UserCodeType,
            DeviceCodeLifetime = client.DeviceCodeLifetime,
            CibaLifetime = client.CibaLifetime,
            PollingInterval = client.PollingInterval,

            // Collections
            AllowedGrantTypes = client.AllowedGrantTypes?.AsReadOnly() ?? [],
            AllowedScopes = client.AllowedScopes?.AsReadOnly() ?? [],
            RedirectUris = client.RedirectUris?.AsReadOnly() ?? [],
            PostLogoutRedirectUris = client.PostLogoutRedirectUris?.AsReadOnly() ?? [],
            AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms?.AsReadOnly() ?? [],
            IdentityProviderRestrictions = client.IdentityProviderRestrictions?.AsReadOnly() ?? [],
            AllowedCorsOrigins = client.AllowedCorsOrigins?.AsReadOnly() ?? [],

            ClientSecrets = existingSecrets,

            Claims = client.Claims?
                .Select(c => new ClientDso.ClaimDso(c.Type, c.Value, c.ValueType))
                .ToList() ?? [],

            ExtendedAttributeValues = EavPropertyMapper.SerializeFromCollection(client.ExtendedProperties)
        };

    internal static Secret MapToIsSecret(CreateClientSecret secret)
    {
        var algorithm = secret.HashAlgorithm ?? SecretHashAlgorithm.Sha256;
        return new Secret
        {
            Value = HashSecret(secret.PlaintextValue, algorithm),
            Description = secret.Description,
            Expiration = secret.Expiration,
            Type = secret.Type ?? IdentityServerConstants.SecretTypes.SharedSecret
        };
    }

    internal static IReadOnlyList<Secret> MapToIsSecrets(IReadOnlyList<CreateClientSecret>? secrets) =>
        secrets?.Select(MapToIsSecret).ToList().AsReadOnly() ?? [];

    private static ClientDso.SecretDso MapToSecretDso(CreateClientSecret secret)
    {
        var algorithm = secret.HashAlgorithm ?? SecretHashAlgorithm.Sha256;
        var algorithmName = algorithm == SecretHashAlgorithm.Sha512 ? "SHA512" : "SHA256";

        return new ClientDso.SecretDso(
            Id: UuidV7.New().Value,
            Value: HashSecret(secret.PlaintextValue, algorithm),
            Description: secret.Description,
            Expiration: secret.Expiration,
            Type: secret.Type ?? IdentityServerConstants.SecretTypes.SharedSecret,
            HashAlgorithm: algorithmName);
    }

    private static ClientConfiguration MapToConfiguration(ClientDso.V1 dso) =>
        new()
        {
            ClientId = dso.ClientId,
            Enabled = dso.Enabled,

            // Display
            ClientName = dso.ClientName,
            Description = dso.Description,
            ClientUri = dso.ClientUri,
            LogoUri = dso.LogoUri,

            // Authentication
            RequireClientSecret = dso.RequireClientSecret,
            RequirePkce = dso.RequirePkce,
            AllowPlainTextPkce = dso.AllowPlainTextPkce,
            RequireRequestObject = dso.RequireRequestObject,
            RequireDPoP = dso.RequireDPoP,
            DPoPValidationMode = (DPoPTokenExpirationValidationMode)dso.DPoPValidationMode,
            DPoPClockSkew = TimeSpan.FromTicks(dso.DPoPClockSkewTicks),

            // Consent
            RequireConsent = dso.RequireConsent,
            AllowRememberConsent = dso.AllowRememberConsent,
            ConsentLifetime = dso.ConsentLifetime,

            // Tokens
            AllowAccessTokensViaBrowser = dso.AllowAccessTokensViaBrowser,
            AllowOfflineAccess = dso.AllowOfflineAccess,
            AccessTokenType = (AccessTokenType)dso.AccessTokenType,
            IncludeJwtId = dso.IncludeJwtId,
            IdentityTokenLifetime = dso.IdentityTokenLifetime,
            AccessTokenLifetime = dso.AccessTokenLifetime,
            AuthorizationCodeLifetime = dso.AuthorizationCodeLifetime,

            // Refresh
            AbsoluteRefreshTokenLifetime = dso.AbsoluteRefreshTokenLifetime,
            SlidingRefreshTokenLifetime = dso.SlidingRefreshTokenLifetime,
            RefreshTokenUsage = (TokenUsage)dso.RefreshTokenUsage,
            RefreshTokenExpiration = (TokenExpiration)dso.RefreshTokenExpiration,
            UpdateAccessTokenClaimsOnRefresh = dso.UpdateAccessTokenClaimsOnRefresh,

            // Claims metadata
            AlwaysIncludeUserClaimsInIdToken = dso.AlwaysIncludeUserClaimsInIdToken,
            AlwaysSendClientClaims = dso.AlwaysSendClientClaims,
            ClientClaimsPrefix = dso.ClientClaimsPrefix,
            PairWiseSubjectSalt = dso.PairWiseSubjectSalt,

            // Session
            UserSsoLifetime = dso.UserSsoLifetime,
            CoordinateLifetimeWithUserSession = dso.CoordinateLifetimeWithUserSession,

            // Login/Logout
            EnableLocalLogin = dso.EnableLocalLogin,
            FrontChannelLogoutUri = dso.FrontChannelLogoutUri,
            FrontChannelLogoutSessionRequired = dso.FrontChannelLogoutSessionRequired,
            BackChannelLogoutUri = dso.BackChannelLogoutUri,
            BackChannelLogoutSessionRequired = dso.BackChannelLogoutSessionRequired,
            InitiateLoginUri = dso.InitiateLoginUri,

            // PAR
            RequirePushedAuthorization = dso.RequirePushedAuthorization,
            PushedAuthorizationLifetime = dso.PushedAuthorizationLifetime,

            // Device/CIBA
            UserCodeType = dso.UserCodeType,
            DeviceCodeLifetime = dso.DeviceCodeLifetime,
            CibaLifetime = dso.CibaLifetime,
            PollingInterval = dso.PollingInterval,

            // Collections
            AllowedGrantTypes = dso.AllowedGrantTypes.ToList().AsReadOnly(),
            AllowedScopes = dso.AllowedScopes.ToList().AsReadOnly(),
            RedirectUris = dso.RedirectUris.ToList().AsReadOnly(),
            PostLogoutRedirectUris = dso.PostLogoutRedirectUris.ToList().AsReadOnly(),
            AllowedIdentityTokenSigningAlgorithms = dso.AllowedIdentityTokenSigningAlgorithms.ToList().AsReadOnly(),
            IdentityProviderRestrictions = dso.IdentityProviderRestrictions.ToList().AsReadOnly(),
            AllowedCorsOrigins = dso.AllowedCorsOrigins.ToList().AsReadOnly(),

            ExtendedProperties = EavPropertyMapper.DeserializeToCollection(dso.ExtendedAttributeValues).ToList().AsReadOnly(),

            // Secrets — metadata only, no Value exposed
            ClientSecrets = dso.ClientSecrets
                .Select(s => new ClientSecretConfiguration
                {
                    Id = s.Id,
                    Description = s.Description,
                    Expiration = s.Expiration,
                    Type = s.Type
                })
                .ToList()
                .AsReadOnly(),

            Claims = dso.Claims
                .Select(c => new ClientClaimConfiguration
                {
                    Type = c.Type,
                    Value = c.Value,
                    ValueType = c.ValueType
                })
                .ToList()
                .AsReadOnly()
        };

    private static ClientListItem MapToListItem(ClientDso.V1 dso) =>
        new()
        {
            Id = dso.Id,
            ClientId = dso.ClientId,
            ClientName = dso.ClientName,
            Enabled = dso.Enabled,
            Description = dso.Description,
            AllowedGrantTypes = [.. dso.AllowedGrantTypes],
            AllowedScopeCount = dso.AllowedScopes.Count,
            RedirectUriCount = dso.RedirectUris.Count
        };

    // === Secret Hashing ===

    private static string HashSecret(string plaintext, SecretHashAlgorithm algorithm)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        return algorithm switch
        {
            SecretHashAlgorithm.Sha512 => Convert.ToBase64String(SHA512.HashData(bytes)),
            _ => Convert.ToBase64String(SHA256.HashData(bytes))
        };
    }
}
