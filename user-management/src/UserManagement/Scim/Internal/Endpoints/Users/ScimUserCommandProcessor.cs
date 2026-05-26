// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Profiles.Internal.Storage;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UserAuthenticators = Duende.UserManagement.Authentication.Internal.UserAuthenticators;
using UserProfile = Duende.UserManagement.Profiles.Internal.UserProfile;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Users;

internal sealed class ScimUserCommandProcessor(
    UserProfileRepository profileRepo,
    AttributeSchemaRepository schemaRepo,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    IEnumerable<IDuendePlatformFeature> features,
    IServiceProvider serviceProvider,
    IUserAdmin userAdmin,
    ILogger<ScimUserCommandProcessor> logger,
    TimeProvider timeProvider,
    UserAuthenticatorsRepository? authenticatorsRepo = null,
    PlainTextPasswordFactory? passwordFactory = null,
    PasswordHashAlgorithms? passwordHashAlgorithms = null)
{
    private bool IsAuthenticationEnabled => features.OfType<UserAuthenticationFeature>().Any();

    internal async Task<ScimOperationResult> CreateAsync(ScimUserRequest? body, Ct ct)
    {
        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        if (body.Schemas is not null &&
            !body.Schemas.Contains(ScimConstants.UserSchemaUrn, StringComparer.OrdinalIgnoreCase))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax,
                $"Schemas must include '{ScimConstants.UserSchemaUrn}'.");
        }

        if (string.IsNullOrWhiteSpace(body.UserName))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "userName is required.");
        }

        if (body.Password is not null && !IsAuthenticationEnabled)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "password is not supported when user authentication is not enabled.");
        }

        var createResult = await TryCreateUserAsync(body, ct);
        if (!createResult.Success)
        {
            return createResult.Error;
        }

        var version = 1;
        var resource = ScimUserMapper.MapToResource(createResult.Value, version, serverUrls.BaseUrl, scimOptions.Value.Route);
        var newUserId = createResult.Value.SubjectId.ToString();
        logger.ScimCreateUserSucceeded(LogLevel.Information, newUserId);
        return ScimOperationResult.Created(
            resource,
            resource.Meta.Location,
            ((ScimETag)version).ToHeaderValue(),
            newUserId);
    }

    internal async Task<ScimOperationResult> ReplaceAsync(string id, ScimUserRequest? body, string? ifMatch, Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return ScimOperationResult.Error(404, "User not found.");
        }

        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        var subjectId = UserSubjectId.Create(guid.ToString());
        var profileExistingResult = await profileRepo.TryReadAsync(subjectId, ct);
        if (profileExistingResult is null)
        {
            if (IsAuthenticationEnabled)
            {
                var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
                var authExists = await authRepo.TryReadAsync(subjectId, ct);
                if (authExists is null)
                {
                    logger.ScimReplaceUserNotFound(LogLevel.Information, id);
                    return ScimOperationResult.Error(404, "User not found.");
                }
            }
            else
            {
                logger.ScimReplaceUserNotFound(LogLevel.Information, id);
                return ScimOperationResult.Error(404, "User not found.");
            }
        }

        int? profileCurrentVersion = profileExistingResult is not null ? profileExistingResult.Value.Version : null;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, profileCurrentVersion);
        if (preconditionError is not null)
        {
            logger.ScimReplaceUserPreconditionFailed(LogLevel.Information, id);
            return preconditionError;
        }

        var schemaResult = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct);
        var schema = schemaResult?.AttributeSchema;

        if (string.IsNullOrEmpty(body.UserName))
        {
            logger.ScimReplaceUserValidationFailed(LogLevel.Information, id, "userName is required.");
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "userName is required.");
        }

        var mapping = ScimRequestMapper.Map(body, schema);
        if (!mapping.IsSuccess)
        {
            logger.ScimReplaceUserValidationFailed(LogLevel.Information, id, mapping.ErrorDetail ?? string.Empty);
            return ScimOperationResult.Error(400, mapping.ErrorScimType, mapping.ErrorDetail);
        }

        var currentSchemaForReplace = schemaResult?.AttributeSchema ?? AttributeSchema.Empty;
        if (!SchemaFreshnessCheck.IsValid(mapping.Attributes!, currentSchemaForReplace, logger))
        {
            return ScimOperationResult.Error(409, "Schema version mismatch. Please retry with the current schema.");
        }

        if (profileExistingResult is not null)
        {
            var (existingProfile, version) = profileExistingResult.Value;
            existingProfile.ReplaceAttributes(mapping.Attributes!);

            if (mapping.UserName is not null)
            {
                existingProfile.SetUserName(mapping.UserName.Value);
            }

            var profileUpdateResult = await profileRepo.UpdateAsync(existingProfile, version, ct);
            switch (profileUpdateResult)
            {
                case UpdateResult.Success:
                    break;
                case UpdateResult.KeyConflict:
                    return ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                        "A user with the same userName or unique attribute already exists.");
                default:
                    logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
                    return ScimOperationResult.Error(500, "An unexpected error occurred while updating the user profile.");
            }
        }
        else
        {
            var newProfile = new UserProfile(subjectId, mapping.Attributes!);

            if (mapping.UserName is not null)
            {
                newProfile.SetUserName(mapping.UserName.Value);
            }

            var createResult = await profileRepo.CreateAsync(newProfile, ct);
            switch (createResult)
            {
                case CreateResult.Success:
                    break;
                case CreateResult.KeyConflict:
                    return ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                        "A user with the same userName or unique attribute already exists.");
                default:
                    return ScimOperationResult.Error(500, "An unexpected error occurred while creating the user profile.");
            }
        }

        if (IsAuthenticationEnabled)
        {
            var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
            var authExistingResult = await authRepo.TryReadAsync(subjectId, ct);

            if (authExistingResult is not null)
            {
                var (existingAuth, authCurrentVersion) = authExistingResult.Value;
                existingAuth.SetUserName(mapping.UserName!.Value);

                var authUpdateResult = await authRepo.UpdateAsync(existingAuth, authCurrentVersion, ct);
                switch (authUpdateResult)
                {
                    case UpdateResult.Success:
                        break;
                    case UpdateResult.DoesNotExist:
                        return ScimOperationResult.Error(404, "User not found.");
                    case UpdateResult.UnexpectedVersion:
                        return ScimOperationResult.Error(412, "Precondition failed: ETag mismatch.");
                    case UpdateResult.KeyConflict:
                        return ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                            "A user with the same userName or unique attribute already exists.");
                    default:
                        logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
                        return ScimOperationResult.Error(500, "An unexpected error occurred while updating the user.");
                }
            }
        }

        var profileReadResult = await profileRepo.TryReadAsync(subjectId, ct);
        if (profileReadResult is null)
        {
            return ScimOperationResult.Error(500, "Failed to read user after update.");
        }

        var (updatedProfile, newProfileVersion) = profileReadResult.Value;
        var resource = ScimUserMapper.MapToResource(updatedProfile, newProfileVersion, serverUrls.BaseUrl, scimOptions.Value.Route);
        logger.ScimReplaceUserSucceeded(LogLevel.Information, id);
        return ScimOperationResult.Ok(resource, ((ScimETag)newProfileVersion).ToHeaderValue(), id);
    }

    internal async Task<ScimOperationResult> DeleteAsync(string id, string? ifMatch, Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return ScimOperationResult.Error(404, "User not found.");
        }

        var subjectId = UserSubjectId.Create(guid.ToString());
        var profileResult = await profileRepo.TryReadAsync(subjectId, ct);
        if (profileResult is null)
        {
            if (IsAuthenticationEnabled)
            {
                var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
                var authExists = await authRepo.TryReadAsync(subjectId, ct);
                if (authExists is null)
                {
                    logger.ScimDeleteUserNotFound(LogLevel.Information, id);
                    return ScimOperationResult.Error(404, "User not found.");
                }
            }
            else
            {
                logger.ScimDeleteUserNotFound(LogLevel.Information, id);
                return ScimOperationResult.Error(404, "User not found.");
            }
        }

        int? currentVersion = profileResult is not null ? profileResult.Value.Version : null;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, currentVersion);
        if (preconditionError is not null)
        {
            logger.ScimDeleteUserPreconditionFailed(LogLevel.Information, id);
            return preconditionError;
        }

        _ = await userAdmin.TryRemoveAsync(subjectId, ct);

        logger.ScimDeleteUserSucceeded(LogLevel.Information, id);
        return ScimOperationResult.NoContent();
    }

    internal async Task<ScimOperationResult> PatchAsync(string id, ScimPatchRequest? body, string? ifMatch, Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return ScimOperationResult.Error(404, "User not found.");
        }

        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        if (body.Operations is not { Count: > 0 })
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "At least one operation is required.");
        }

        if (body.Schemas is not null &&
            !body.Schemas.Contains(ScimConstants.PatchOpSchemaUrn, StringComparer.OrdinalIgnoreCase))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax,
                $"Schemas must include '{ScimConstants.PatchOpSchemaUrn}'.");
        }

        var subjectId = UserSubjectId.Create(guid.ToString());
        var profileExistingResult = await profileRepo.TryReadAsync(subjectId, ct);
        if (profileExistingResult is null)
        {
            logger.ScimPatchUserNotFound(LogLevel.Information, id);
            return ScimOperationResult.Error(404, "User not found.");
        }

        var profileCurrentVersion = profileExistingResult.Value.Version;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, profileCurrentVersion);
        if (preconditionError is not null)
        {
            logger.ScimPatchUserPreconditionFailed(LogLevel.Information, id);
            return preconditionError;
        }

        var schemaResult = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct);
        var schema = schemaResult?.AttributeSchema;
        var profile = profileExistingResult.Value.UserProfile;

        UserAuthenticators? authenticators = null;
        var authCurrentVersion = -1;
        if (IsAuthenticationEnabled)
        {
            var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
            var authExistingResult = await authRepo.TryReadAsync(subjectId, ct);
            if (authExistingResult is not null)
            {
                (authenticators, authCurrentVersion) = authExistingResult.Value;
            }
        }

        var effectiveSchema = schema ?? AttributeSchema.Empty;
        var attributes = new AttributeValueCollection(effectiveSchema);
        foreach (var attr in profile.Attributes.Values)
        {
            if (effectiveSchema.AttributeDefinitions.ContainsKey(attr.Code))
            {
                attributes.Set(attr);
            }
        }
        foreach (var op in body.Operations)
        {
            var applyResult = ApplyOperation(op, authenticators, profile, attributes, schema);
            if (!applyResult.Success)
            {
                return applyResult.Error!;
            }
        }

        var validatedAttributes = attributes.Validate();
        var currentSchemaForPatch = effectiveSchema;
        if (!SchemaFreshnessCheck.IsValid(validatedAttributes, currentSchemaForPatch, logger))
        {
            return ScimOperationResult.Error(409, "Schema version mismatch. Please retry with the current schema.");
        }

        profile.ReplaceAttributes(validatedAttributes);

        var profileUpdateResult = await profileRepo.UpdateAsync(profile, profileCurrentVersion, ct);
        switch (profileUpdateResult)
        {
            case UpdateResult.Success:
                break;
            case UpdateResult.KeyConflict:
                return ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                    "A user with the same userName or unique attribute already exists.");
            default:
                logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
                return ScimOperationResult.Error(500, "An unexpected error occurred while updating the user profile.");
        }

        if (IsAuthenticationEnabled && authenticators is not null)
        {
            var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
            var authUpdateResult = await authRepo.UpdateAsync(authenticators, authCurrentVersion, ct);
            switch (authUpdateResult)
            {
                case UpdateResult.Success:
                    break;
                case UpdateResult.DoesNotExist:
                    return ScimOperationResult.Error(404, "User not found.");
                case UpdateResult.UnexpectedVersion:
                    return ScimOperationResult.Error(412, "Precondition failed: ETag mismatch.");
                case UpdateResult.KeyConflict:
                    return ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                        "A user with the same userName or unique attribute already exists.");
                default:
                    logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
                    return ScimOperationResult.Error(500, "An unexpected error occurred while updating the user.");
            }
        }

        var newProfileVersion = profileCurrentVersion + 1;
        var resource = ScimUserMapper.MapToResource(profile, newProfileVersion, serverUrls.BaseUrl, scimOptions.Value.Route);
        logger.ScimPatchUserSucceeded(LogLevel.Information, id);
        return ScimOperationResult.Ok(resource, ((ScimETag)newProfileVersion).ToHeaderValue(), id);
    }

    private async Task<Result<UserProfile, ScimOperationResult>> TryCreateUserAsync(ScimUserRequest body, CancellationToken ct)
    {
        var schemaResult = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct);
        var schema = schemaResult?.AttributeSchema;

        var mapping = ScimRequestMapper.Map(body, schema);
        if (!mapping.IsSuccess)
        {
            logger.ScimCreateUserValidationFailed(LogLevel.Information, mapping.ErrorDetail ?? string.Empty);
            return Result.Create(ScimOperationResult.Error(400, mapping.ErrorScimType, mapping.ErrorDetail));
        }

        var currentSchemaForCreate = schema ?? AttributeSchema.Empty;
        if (!SchemaFreshnessCheck.IsValid(mapping.Attributes!, currentSchemaForCreate, logger))
        {
            return Result.Create(ScimOperationResult.Error(409, "Schema version mismatch. Please retry with the current schema."));
        }

        var createAuthenticators = IsAuthenticationEnabled && mapping.Password != null;
        var subjectId = UserSubjectId.New();
        var profile = new UserProfile(subjectId, mapping.Attributes!);

        if (mapping.UserName is not null)
        {
            profile.SetUserName(mapping.UserName.Value);
        }

        var profileCreateResult = await profileRepo.CreateAsync(profile, ct);
        switch (profileCreateResult)
        {
            case CreateResult.Success:
                break;
            case CreateResult.KeyConflict:
                return Result.Create(ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                    "A user with the same userName or unique attribute already exists."));
            default:
                logger.ScimCreateUserRepositoryFailed(LogLevel.Warning);
                return Result.Create(ScimOperationResult.Error(500, "An unexpected error occurred while creating the user profile."));
        }

        if (createAuthenticators)
        {
            var authResult = await TryCreateAuthenticatorsAsync(subjectId, mapping, ct);
            if (!authResult.Success)
            {
                _ = await userAdmin.TryRemoveAsync(subjectId, ct);
                return Result.Create(authResult.Error);
            }
        }

        return profile;
    }

    private async Task<Result<CreateResult, ScimOperationResult>> TryCreateAuthenticatorsAsync(
        UserSubjectId subjectId,
        ScimRequestMapper.MappingResult mapping,
        Ct ct)
    {
        if (authenticatorsRepo == null)
        {
            throw new InvalidOperationException("Authenticators repo isn't available but authentication is enabled?");
        }

        if (passwordFactory == null)
        {
            throw new InvalidOperationException("PasswordFactory isn't available but authentication is enabled?");
        }

        var authenticators = new UserAuthenticators(subjectId, [], []);

        if (mapping.UserName is not null)
        {
            authenticators.SetUserName(mapping.UserName.Value);
        }

        if (mapping.Password is not null)
        {
            var passwordResult = await passwordFactory.CreateAsync(subjectId, mapping.Password, ct);
            if (passwordResult is not PasswordCreationResult.Success { Password: var plainTextPassword })
            {
                var detail = passwordResult is PasswordCreationResult.Failed { Errors: var errors }
                    ? string.Join(" ", errors)
                    : "The provided password does not meet requirements.";
                return Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, detail));
            }

            _ = authenticators.TrySetPassword(plainTextPassword,
                passwordHashAlgorithms?.Preferred
                    ?? throw new InvalidOperationException("PasswordHashAlgorithms is not registered."),
                timeProvider);
        }

        var result = await authenticatorsRepo.CreateAsync(authenticators, ct);
        return result switch
        {
            CreateResult.Success => result,
            CreateResult.KeyConflict => Result.Create(ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                "A user with the same userName or unique attribute already exists.")),
            _ => Result.Create(ScimOperationResult.Error(500, "An unexpected error occurred while creating the user."))
        };
    }

    private sealed class ApplyResult
    {
        internal static readonly ApplyResult Ok = new() { Success = true };
        internal bool Success { get; private init; }
        internal ScimOperationResult? Error { get; private init; }
        internal static ApplyResult FromError(ScimOperationResult error) => new() { Success = false, Error = error };
    }

    private ApplyResult ApplyOperation(
        ScimPatchOperation op,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
#pragma warning disable CA1308
        var opLower = op.Op?.ToLowerInvariant();
#pragma warning restore CA1308
        return opLower switch
        {
            ScimConstants.PatchOps.Add => ApplyAdd(op, authenticators, profile, attributes, schema),
            ScimConstants.PatchOps.Replace => ApplyReplace(op, authenticators, profile, attributes, schema),
            ScimConstants.PatchOps.Remove => ApplyRemove(op, authenticators, profile, attributes, schema),
            _ => ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Unsupported operation '{op.Op}'. Supported: {ScimConstants.PatchOps.Add}, {ScimConstants.PatchOps.Replace}, {ScimConstants.PatchOps.Remove}."))
        };
    }

    private ApplyResult ApplyAdd(
        ScimPatchOperation op,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Path is null)
        {
            return ApplyValueObjectKeys(op, authenticators, profile, attributes, schema);
        }

        var valueError = RequireValue(op);
        if (!valueError.Success)
        {
            return valueError;
        }

        var existingComplexSnapshot = SnapshotComplexAttribute(op.Path, attributes, schema);
        var setResult = ApplyAttributeValue(op.Path, op.Value!.Value, authenticators, profile, attributes, schema);
        if (!setResult.Success)
        {
            return setResult;
        }

        return existingComplexSnapshot is not null
            ? MergeComplexAttribute(op.Path, existingComplexSnapshot, attributes, schema!)
            : ApplyResult.Ok;
    }

    private ApplyResult ApplyReplace(
        ScimPatchOperation op,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Path is null)
        {
            return ApplyValueObjectKeys(op, authenticators, profile, attributes, schema);
        }

        var valueError = RequireValue(op);
        if (!valueError.Success)
        {
            return valueError;
        }

        return ApplyAttributeValue(op.Path, op.Value!.Value, authenticators, profile, attributes, schema);
    }

    private ApplyResult ApplyValueObjectKeys(
        ScimPatchOperation op,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Value is not { ValueKind: JsonValueKind.Object } valueObj)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "When 'path' is omitted, 'value' must be a JSON object."));
        }

        foreach (var prop in valueObj.EnumerateObject())
        {
            var attrResult = ApplyAttributeValue(prop.Name, prop.Value, authenticators, profile, attributes, schema);
            if (!attrResult.Success)
            {
                return attrResult;
            }
        }

        return ApplyResult.Ok;
    }

    private static ApplyResult RequireValue(ScimPatchOperation op) =>
        op.Value.HasValue
            ? ApplyResult.Ok
            : ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Operation '{op.Op}' on path '{op.Path}' requires a value."));

    private static IReadOnlyDictionary<string, object>? SnapshotComplexAttribute(
        string path,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (schema is null || path.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        if (!AttributeCode.TryCreate(path, out var attrName) ||
            !schema.AttributeDefinitions.TryGetValue(attrName, out var definition) ||
            definition.AttributeType is not ComplexAttributeType)
        {
            return null;
        }

        if (attributes.TryGet(definition, out var existing) &&
            existing.UntypedValue is IReadOnlyDictionary<string, object> dict)
        {
            return dict;
        }

        return null;
    }

    private static ApplyResult MergeComplexAttribute(
        string path,
        IReadOnlyDictionary<string, object> existingSnapshot,
        AttributeValueCollection attributes,
        AttributeSchema schema)
    {
        if (!AttributeCode.TryCreate(path, out var attrName) ||
            !schema.AttributeDefinitions.TryGetValue(attrName, out var definition) ||
            !attributes.TryGet(definition, out var newAttr) ||
            newAttr.UntypedValue is not IReadOnlyDictionary<string, object> newDict)
        {
            return ApplyResult.Ok;
        }

        var mergedDict = new Dictionary<string, object>(existingSnapshot, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in newDict)
        {
            mergedDict[k] = v;
        }

        try
        {
            attributes.Set(attrName, (IReadOnlyDictionary<string, object>)mergedDict);
        }
        catch (ArgumentException)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid value for attribute '{path}'."));
        }

        return ApplyResult.Ok;
    }

    private ApplyResult ApplyRemove(
        ScimPatchOperation op,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Path is null)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.NoTarget,
                "Operation 'remove' requires a 'path'."));
        }

        if (op.Path.Contains('[', StringComparison.Ordinal) || op.Path.Contains(']', StringComparison.Ordinal))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                "Complex filter path expressions are not supported."));
        }

        if (op.Path.Equals(ScimConstants.Attributes.UserName, StringComparison.OrdinalIgnoreCase))
        {
            if (IsAuthenticationEnabled)
            {
                authenticators?.RemoveUserName();
            }

            profile.RemoveUserName();

            return ApplyResult.Ok;
        }

        if (op.Path.Equals(ScimConstants.Attributes.ExternalId, StringComparison.OrdinalIgnoreCase))
        {
            if (AttributeCode.TryCreate(ScimConstants.ExternalIdAttributeName, out var extIdName))
            {
                _ = attributes.Remove(extIdName);
            }

            return ApplyResult.Ok;
        }

        if (op.Path.Contains('.', StringComparison.Ordinal))
        {
            if (schema is null)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    $"Attribute '{op.Path}' is not available without a registered schema."));
            }

            return ApplyDotNotationRemove(op.Path, attributes, schema);
        }

        if (!AttributeCode.TryCreate(op.Path, out var attrName))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Invalid attribute path: '{op.Path}'."));
        }

        _ = attributes.Remove(attrName);
        return ApplyResult.Ok;
    }

    private ApplyResult ApplyAttributeValue(
        string path,
        JsonElement value,
        UserAuthenticators? authenticators,
        UserProfile profile,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (path.Contains('[', StringComparison.Ordinal) || path.Contains(']', StringComparison.Ordinal))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                "Complex filter path expressions are not supported."));
        }

        if (path.Equals(ScimConstants.Attributes.UserName, StringComparison.OrdinalIgnoreCase))
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                if (IsAuthenticationEnabled)
                {
                    authenticators?.RemoveUserName();
                }

                profile.RemoveUserName();

                return ApplyResult.Ok;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "userName must be a string."));
            }

            var userNameStr = value.GetString() ?? string.Empty;
            if (!UserName.TryCreate(userNameStr, out var parsedName))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid userName value: '{userNameStr}'."));
            }

            if (IsAuthenticationEnabled)
            {
                authenticators?.SetUserName(parsedName.Value);
            }

            profile.SetUserName(parsedName.Value);

            return ApplyResult.Ok;
        }

        if (path.Equals(ScimConstants.Attributes.ExternalId, StringComparison.OrdinalIgnoreCase))
        {
            if (schema is null)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "externalId attribute is not available without a registered schema."));
            }

            if (!AttributeCode.TryCreate(ScimConstants.ExternalIdAttributeName, out var extIdName))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "externalId attribute name is invalid."));
            }

            if (!schema.AttributeDefinitions.ContainsKey(extIdName))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "externalId is not defined in the schema."));
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "externalId must be a string."));
            }

            attributes.Set(extIdName, value.GetString()!);
            return ApplyResult.Ok;
        }

        if (path.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("schemas", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("meta", StringComparison.OrdinalIgnoreCase))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.Mutability,
                $"Attribute '{path}' is read-only and cannot be modified."));
        }

        if (schema is null)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{path}' is not available without a registered schema."));
        }

        if (path.Contains('.', StringComparison.Ordinal))
        {
            return ApplyDotNotationAttributeValue(path, value, attributes, schema);
        }

        if (!AttributeCode.TryCreate(path, out var attrName))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Invalid attribute path: '{path}'."));
        }

        if (!schema.AttributeDefinitions.TryGetValue(attrName, out var definition))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{path}' is not defined in the schema."));
        }

        try
        {
            var set = ScimRequestMapper.TrySetJsonElement(value, definition, definition, attributes);
            if (!set)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid value type for attribute '{path}': expected {definition.AttributeType.GetType().Name}."));
            }
        }
        catch (ArgumentException)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid value for attribute '{path}'."));
        }
        return ApplyResult.Ok;
    }

    private static ApplyResult ApplyDotNotationAttributeValue(
        string path,
        JsonElement value,
        AttributeValueCollection attributes,
        AttributeSchema schema)
    {
        var dotIndex = path.IndexOf('.', StringComparison.Ordinal);
        var parentPathRaw = path[..dotIndex];
        var subPathRaw = path[(dotIndex + 1)..];

        if (!AttributeCode.TryCreate(parentPathRaw, out var parentAttrName))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Invalid attribute path: '{path}'."));
        }

        if (!schema.AttributeDefinitions.TryGetValue(parentAttrName, out var parentDefinition))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{parentPathRaw}' is not defined in the schema."));
        }

        if (parentDefinition.AttributeType is not ComplexAttributeType complexType)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{parentPathRaw}' is not a complex type and does not support dot-notation paths."));
        }

        if (subPathRaw.Contains('.', StringComparison.Ordinal))
        {
            return ApplyNestedDotNotation(path, parentAttrName, subPathRaw, value, complexType, attributes, schema);
        }

        if (!complexType.TryGetProperty(subPathRaw, out var canonicalSubKey, out var subAttrType))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Sub-attribute '{subPathRaw}' is not defined on '{parentPathRaw}'."));
        }

        var existingDict = ReadExistingComplexDict(parentAttrName, attributes);
        var convertResult = ConvertJsonValueForSubType(value, subAttrType.Type, path);
        if (!convertResult.Success)
        {
            return ApplyResult.FromError(convertResult.Error);
        }

        existingDict[canonicalSubKey!.Value] = convertResult.Value;

        try
        {
            attributes.Set(parentAttrName, (IReadOnlyDictionary<string, object>)existingDict);
        }
        catch (ArgumentException)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid value for attribute '{path}'."));
        }

        return ApplyResult.Ok;
    }

    private static ApplyResult ApplyNestedDotNotation(
        string fullPath,
        AttributeCode parentAttrCode,
        string remainingSubPath,
        JsonElement value,
        ComplexAttributeType complexType,
        AttributeValueCollection attributes,
        AttributeSchema schema)
    {
        var dotIndex = remainingSubPath.IndexOf('.', StringComparison.Ordinal);
        var segmentKey = remainingSubPath[..dotIndex];
        var deeperPath = remainingSubPath[(dotIndex + 1)..];

        if (!complexType.TryGetProperty(segmentKey, out var canonicalSegmentKey, out var segmentProp) ||
            segmentProp.Type is not ComplexAttributeType nestedComplexType)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Sub-attribute '{segmentKey}' on '{parentAttrCode}' is not a complex type."));
        }

        if (!nestedComplexType.TryGetProperty(deeperPath, out var canonicalDeeperKey, out var subAttrProp))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Sub-attribute '{deeperPath}' is not defined on '{segmentKey}'."));
        }

        var outerDict = ReadExistingComplexDict(parentAttrCode, attributes);
        var innerDict = outerDict.TryGetValue(canonicalSegmentKey!.Value, out var existingNested) &&
                        existingNested is IReadOnlyDictionary<string, object> existingNestedDict
            ? new Dictionary<string, object>(existingNestedDict, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var convertResult = ConvertJsonValueForSubType(value, subAttrProp.Type, fullPath);
        if (!convertResult.Success)
        {
            return ApplyResult.FromError(convertResult.Error);
        }

        innerDict[canonicalDeeperKey!.Value] = convertResult.Value;
        outerDict[canonicalSegmentKey!.Value] = (IReadOnlyDictionary<string, object>)innerDict;

        try
        {
            attributes.Set(parentAttrCode, (IReadOnlyDictionary<string, object>)outerDict);
        }
        catch (ArgumentException)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid value for attribute '{fullPath}'."));
        }

        return ApplyResult.Ok;
    }

    private static ApplyResult ApplyDotNotationRemove(
        string path,
        AttributeValueCollection attributes,
        AttributeSchema schema)
    {
        var dotIndex = path.IndexOf('.', StringComparison.Ordinal);
        var parentPathRaw = path[..dotIndex];
        var subPathRaw = path[(dotIndex + 1)..];

        if (!AttributeCode.TryCreate(parentPathRaw, out var parentAttrName))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Invalid attribute path: '{path}'."));
        }

        if (!schema.AttributeDefinitions.TryGetValue(parentAttrName, out var parentDefinition))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{parentPathRaw}' is not defined in the schema."));
        }

        if (parentDefinition.AttributeType is not ComplexAttributeType complexType)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Attribute '{parentPathRaw}' is not a complex type and does not support dot-notation paths."));
        }

        if (!complexType.TryGetProperty(subPathRaw, out _, out _))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                $"Sub-attribute '{subPathRaw}' is not defined on '{parentPathRaw}'."));
        }

        if (!attributes.TryGet(parentAttrName, out var existingAttr) ||
            existingAttr.UntypedValue is not IReadOnlyDictionary<string, object> existingDict)
        {
            return ApplyResult.Ok;
        }

        var updatedDict = new Dictionary<string, object>(existingDict, StringComparer.OrdinalIgnoreCase);
        _ = updatedDict.Remove(subPathRaw);

        if (updatedDict.Count == 0)
        {
            _ = attributes.Remove(parentAttrName);
            return ApplyResult.Ok;
        }

        try
        {
            attributes.Set(parentAttrName, (IReadOnlyDictionary<string, object>)updatedDict);
        }
        catch (ArgumentException)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid operation removing sub-attribute '{path}'."));
        }

        return ApplyResult.Ok;
    }

    private static Dictionary<string, object> ReadExistingComplexDict(
        AttributeCode parentAttrCode,
        AttributeValueCollection attributes)
    {
        if (attributes.TryGet(parentAttrCode, out var existingAttr) &&
            existingAttr.UntypedValue is IReadOnlyDictionary<string, object> existingDict)
        {
            return new Dictionary<string, object>(existingDict, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static Result<object, ScimOperationResult> ConvertJsonValueForSubType(JsonElement value, AttributeType subAttrType, string path)
    {
        if (subAttrType is ScalarAttributeType scalarType)
        {
            var converted = ConvertScalarSubValue(value, scalarType.DataType);
            if (converted is null)
            {
                return Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid value type for sub-attribute '{path}'."));
            }

            return converted;
        }

        if (subAttrType is ComplexAttributeType subComplex && value.ValueKind == JsonValueKind.Object)
        {
            var dict = ConvertComplexSubDict(value, subComplex);
            if (dict is null)
            {
                return Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid complex value for sub-attribute '{path}'."));
            }

            return dict;
        }

        if (subAttrType is ListAttributeType subList && value.ValueKind == JsonValueKind.Array)
        {
            var list = ConvertListSubList(value, subList);
            if (list is null)
            {
                return Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid list value for sub-attribute '{path}'."));
            }

            return list;
        }

        return Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
            $"Invalid value type for sub-attribute '{path}'."));
    }

    private static object? ConvertScalarSubValue(JsonElement element, ScalarDataType dataType) =>
        dataType switch
        {
            ScalarDataType.Boolean when element.ValueKind == JsonValueKind.True => (object)true,
            ScalarDataType.Boolean when element.ValueKind == JsonValueKind.False => false,
            ScalarDataType.Integer when element.ValueKind == JsonValueKind.Number => element.TryGetInt32(out var i) ? (object)i : null,
            ScalarDataType.Decimal when element.ValueKind == JsonValueKind.Number => element.TryGetDecimal(out var dec) ? (object)dec : null,
            ScalarDataType.String when element.ValueKind == JsonValueKind.String => element.GetString()!,
            ScalarDataType.Date when element.ValueKind == JsonValueKind.String =>
                DateOnly.TryParse(element.GetString(), out var d) ? (object)d : null,
            ScalarDataType.DateTime when element.ValueKind == JsonValueKind.String =>
                DateTimeOffset.TryParse(element.GetString(), out var dt) ? (object)dt : null,
            _ => null
        };

    private static Dictionary<string, object>? ConvertComplexSubDict(JsonElement element, ComplexAttributeType complexType)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in element.EnumerateObject())
        {
            if (!complexType.TryGetProperty(prop.Name, out var canonicalKey, out var propEntry) || propEntry.Type is not ScalarAttributeType scalarPropType)
            {
                return null;
            }

            var converted = ConvertScalarSubValue(prop.Value, scalarPropType.DataType);
            if (converted is null)
            {
                return null;
            }

            result[canonicalKey!.Value] = converted;
        }

        return result;
    }

    private static List<object>? ConvertListSubList(JsonElement element, ListAttributeType listType)
    {
        var result = new List<object>();

        foreach (var item in element.EnumerateArray())
        {
            if (listType.ElementType is ComplexAttributeType elemComplex)
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var dict = ConvertComplexSubDict(item, elemComplex);
                if (dict is null)
                {
                    return null;
                }

                result.Add(dict);
            }
            else if (listType.ElementType is ScalarAttributeType elemScalar)
            {
                var converted = ConvertScalarSubValue(item, elemScalar.DataType);
                if (converted is null)
                {
                    return null;
                }

                result.Add(converted);
            }
            else
            {
                return null;
            }
        }

        return result;
    }
}
