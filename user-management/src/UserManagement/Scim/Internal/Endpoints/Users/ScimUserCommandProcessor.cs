// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Internal.Storage;
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
    UserManagementLicenseValidator licenseValidator,
    IStoreFactory storeFactory,
    UserRepository userRepository,
    UserAuthenticatorsRepository? authenticatorsRepo = null,
    ValidatedPlainTextPasswordFactory? passwordFactory = null,
    PasswordHashAlgorithms? passwordHashAlgorithms = null)
{
    private readonly IStoreFactory _storeFactory = storeFactory;
    private readonly UserRepository _userRepository = userRepository;

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

    /// <summary>
    /// SCIM PUT — full replacement of a user resource. Orchestrates three phases:
    /// validate the request, build the batch of store operations, and execute them atomically.
    /// </summary>
    internal async Task<ScimOperationResult> ReplaceAsync(string id, ScimUserRequest? body, string? ifMatch, Ct ct)
    {
        // 1. Validate: parse id, check body, verify user exists, check ETag, map SCIM → attributes
        var validated = await ValidateReplaceAsync(id, body, ifMatch, ct);
        if (validated.Error is not null)
        {
            return validated.Error;
        }

        // 2. Build: create store operations for profile, password (if provided), and UserDso
        var built = await BuildReplaceOperationsAsync(validated, ct);
        if (built.Error is not null)
        {
            return built.Error;
        }

        // 3. Execute: run all operations as a single atomic batch
        return await ExecuteReplaceBatchAsync(id, built.Operations, built.UpdatedProfile, built.NewUserDsoVersion, ct);
    }

    /// <summary>
    /// Phase 1: Validates all preconditions for a SCIM PUT request.
    /// <list type="bullet">
    ///   <item>Parses the user id (must be a valid GUID)</item>
    ///   <item>Checks the request body is present and has a userName</item>
    ///   <item>Loads existing profile and UserDso to confirm the user exists</item>
    ///   <item>Enforces the If-Match ETag precondition</item>
    ///   <item>Maps the SCIM request body to internal attribute values</item>
    ///   <item>Verifies the attribute schema version hasn't drifted</item>
    /// </list>
    /// </summary>
    private async Task<ReplaceValidationResult> ValidateReplaceAsync(
        string id, ScimUserRequest? body, string? ifMatch, Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return ReplaceValidationResult.Fail(ScimOperationResult.Error(404, "User not found."));
        }

        if (body is null)
        {
            return ReplaceValidationResult.Fail(
                ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required."));
        }

        var subjectId = UserSubjectId.Create(guid.ToString());
        var profileExistingResult = await profileRepo.TryReadAsync(subjectId, ct);
        var userExistingResult = await _userRepository.TryReadAsync(subjectId, ct);

        if (profileExistingResult is null && userExistingResult is null)
        {
            logger.ScimReplaceUserNotFound(LogLevel.Information, id);
            return ReplaceValidationResult.Fail(ScimOperationResult.Error(404, "User not found."));
        }

        var userDsoVersion = userExistingResult?.Version;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, userDsoVersion);
        if (preconditionError is not null)
        {
            logger.ScimReplaceUserPreconditionFailed(LogLevel.Information, id);
            return ReplaceValidationResult.Fail(preconditionError);
        }

        var schemaResult = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct);

        if (string.IsNullOrWhiteSpace(body.UserName))
        {
            logger.ScimReplaceUserValidationFailed(LogLevel.Information, id, "userName is required.");
            return ReplaceValidationResult.Fail(
                ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "userName is required."));
        }

        var mapping = ScimRequestMapper.Map(body, schemaResult?.AttributeSchema);
        if (!mapping.IsSuccess)
        {
            logger.ScimReplaceUserValidationFailed(LogLevel.Information, id, mapping.ErrorDetail ?? string.Empty);
            return ReplaceValidationResult.Fail(
                ScimOperationResult.Error(400, mapping.ErrorScimType, mapping.ErrorDetail));
        }

        var currentSchema = schemaResult?.AttributeSchema ?? AttributeSchema.Empty;
        if (!SchemaFreshnessCheck.IsValid(mapping.Attributes!, currentSchema, logger))
        {
            return ReplaceValidationResult.Fail(
                ScimOperationResult.Error(409, "Schema version mismatch. Please retry with the current schema."));
        }

        if (mapping.Password is not null && !IsAuthenticationEnabled)
        {
            return ReplaceValidationResult.Fail(
                ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "password is not supported when user authentication is not enabled."));
        }

        return new ReplaceValidationResult(subjectId, mapping, profileExistingResult, userExistingResult);
    }

    /// <summary>
    /// Phase 2: Builds the ordered list of store operations needed to persist the replacement.
    /// <list type="bullet">
    ///   <item>Creates or updates the profile aspect with the new attribute values</item>
    ///   <item>If a password was provided, creates or updates the authenticators aspect</item>
    ///   <item>Creates or updates the root UserDso with references to all modified aspects</item>
    /// </list>
    /// Operations are ordered: UserDso first, then authenticators, then profile aspect last.
    /// </summary>
    private async Task<ReplaceBuildResult> BuildReplaceOperationsAsync(
        ReplaceValidationResult validated, Ct ct)
    {
        // Resolve profile aspect: update existing or create new
        IStoreOperation profileAspectOp;
        int newProfileVersion;
        UserProfile updatedProfile;

        if (validated.ProfileExistingResult is not null)
        {
            var (existingProfile, existingVersion) = validated.ProfileExistingResult.Value;
            existingProfile.ReplaceAttributes(validated.Mapping!.Attributes!);
            profileAspectOp = await profileRepo.UpdateAspectOnlyBatchOperationAsync(existingProfile, existingVersion, ct);
            newProfileVersion = existingVersion + 1;
            updatedProfile = existingProfile;
        }
        else
        {
            var newProfile = new UserProfile(validated.SubjectId!, validated.Mapping!.Attributes!);
            var (createOp, _) = await profileRepo.CreateAspectBatchOperationAsync(newProfile, ct);
            profileAspectOp = createOp;
            newProfileVersion = 1;
            updatedProfile = newProfile;
        }

        // Collect aspect references that will be written into the root UserDso.
        // Each aspect (profile, authenticators) tracks its own version independently.
        var profileAspectRef = UserProfileRepository.GetAspectRef(updatedProfile, newProfileVersion);
        List<UserDso.AspectRef> aspectReferences = [profileAspectRef];
        List<IStoreOperation> operations = [];

        // Optionally handle password: validate, hash, and create/update the authenticators aspect
        if (IsAuthenticationEnabled && validated.Mapping!.Password != null)
        {
            if (authenticatorsRepo is null)
            {
                throw new InvalidOperationException("Authenticators repo is not available but authentication is enabled.");
            }

            var authExistingResult = await authenticatorsRepo.TryReadAsync(validated.SubjectId!, ct);
            if (authExistingResult is not null)
            {
                var (existingAuth, authVersion) = authExistingResult.Value;
                var (pwError, updatedAuth) = await TryApplyPasswordAsync(validated.SubjectId!, validated.Mapping!.Password, existingAuth, ct);
                if (pwError is not null)
                {
                    return ReplaceBuildResult.Fail(pwError);
                }

                operations.Add(authenticatorsRepo.UpdateAspectOnlyBatchOperation(updatedAuth, authVersion));
                aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(updatedAuth, authVersion + 1));
            }
            else
            {
                var newAuth = new UserAuthenticators(validated.SubjectId!, [], []);
                var (pwError, updatedAuth) = await TryApplyPasswordAsync(validated.SubjectId!, validated.Mapping!.Password, newAuth, ct);
                if (pwError is not null)
                {
                    return ReplaceBuildResult.Fail(pwError);
                }

                operations.Add(authenticatorsRepo.CreateAspectBatchOperation(updatedAuth));
                aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(updatedAuth));
            }
        }

        // Build the root UserDso operation — it holds references to all aspect versions.
        // This ensures the UserDso always points to the correct version of each aspect.
        IStoreOperation userOp;
        int newUserDsoVersion;
        if (validated.UserExistingResult is not null)
        {
            var (existingUserDso, userVersion) = validated.UserExistingResult.Value;
            var updatedUserDso = existingUserDso;
            foreach (var aspectRef in aspectReferences)
            {
                updatedUserDso = UserRepository.AddOrUpdateAspectRef(updatedUserDso, aspectRef);
            }

            userOp = UserRepository.UpdateBatchOperation(updatedUserDso, userVersion);
            newUserDsoVersion = userVersion + 1;
        }
        else
        {
            userOp = UserRepository.CreateBatchOperation(validated.SubjectId!, aspectReferences);
            newUserDsoVersion = 1;
        }

        var orderedOperations = new List<IStoreOperation> { userOp };
        orderedOperations.AddRange(operations);
        orderedOperations.Add(profileAspectOp);

        return new ReplaceBuildResult(orderedOperations, updatedProfile, newUserDsoVersion);
    }

    /// <summary>
    /// Phase 3: Executes all store operations as a single atomic batch and returns
    /// the SCIM response. The UserDso version is deterministic (previous + 1), so no
    /// re-read is needed after the batch succeeds.
    /// </summary>
    private async Task<ScimOperationResult> ExecuteReplaceBatchAsync(
        string id,
        List<IStoreOperation> operations,
        UserProfile updatedProfile,
        int newUserDsoVersion,
        Ct ct)
    {
        var batchResult = await (await _storeFactory.GetStore(ct)).ExecuteBatchAsync(operations, [], ct);
        if (!batchResult.Success)
        {
            var mapError = MapBatchError(batchResult);
            if (mapError.StatusCode == 409)
            {
                return mapError;
            }

            logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
            return mapError;
        }

        var resource = ScimUserMapper.MapToResource(updatedProfile, newUserDsoVersion, serverUrls.BaseUrl, scimOptions.Value.Route);
        logger.ScimReplaceUserSucceeded(LogLevel.Information, id);
        return ScimOperationResult.Ok(resource, ((ScimETag)newUserDsoVersion).ToHeaderValue(), id);
    }

    private sealed record ReplaceValidationResult(
        UserSubjectId? SubjectId = default,
        ScimRequestMapper.MappingResult? Mapping = null,
        (UserProfile UserProfile, int Version)? ProfileExistingResult = null,
        (UserDso.V1 User, int Version)? UserExistingResult = null)
    {
        internal ScimOperationResult? Error { get; private init; }

        internal static ReplaceValidationResult Fail(ScimOperationResult error) =>
            new() { Error = error };
    }

    private sealed record ReplaceBuildResult(
        List<IStoreOperation> Operations,
        UserProfile UpdatedProfile,
        int NewUserDsoVersion)
    {
        internal ScimOperationResult? Error { get; private init; }

        internal static ReplaceBuildResult Fail(ScimOperationResult error) =>
            new([], null!, 0) { Error = error };
    }

    internal async Task<ScimOperationResult> DeleteAsync(string id, string? ifMatch, Ct ct)
    {
        if (!Guid.TryParse(id, out var guid))
        {
            return ScimOperationResult.Error(404, "User not found.");
        }

        var subjectId = UserSubjectId.Create(guid.ToString());
        var profileResult = await profileRepo.TryReadAsync(subjectId, ct);
        var userResult = await _userRepository.TryReadAsync(subjectId, ct);

        if (profileResult is null && userResult is null)
        {
            logger.ScimDeleteUserNotFound(LogLevel.Information, id);
            return ScimOperationResult.Error(404, "User not found.");
        }

        var userDsoVersion = userResult?.Version;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, userDsoVersion);
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

        // Read UserDso once for the batch and for the If-Match precondition check
        var userExistingResult = await _userRepository.TryReadAsync(subjectId, ct);

        var userDsoCurrentVersion = userExistingResult?.Version;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, userDsoCurrentVersion);
        if (preconditionError is not null)
        {
            logger.ScimPatchUserPreconditionFailed(LogLevel.Information, id);
            return preconditionError;
        }

        var schemaResult = await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct);
        var schema = schemaResult?.AttributeSchema;
        var profile = profileExistingResult.Value.UserProfile;

        // Read authenticators if authentication is enabled
        UserAuthenticators? authenticators = null;
        var authCurrentVersion = -1;
        var authExists = false;
        if (IsAuthenticationEnabled)
        {
            var authRepo = serviceProvider.GetRequiredService<UserAuthenticatorsRepository>();
            var authExistingResult = await authRepo.TryReadAsync(subjectId, ct);
            if (authExistingResult is not null)
            {
                (authenticators, authCurrentVersion) = authExistingResult.Value;
                authExists = true;
            }
        }

        // Dispatch patch operations — intercept password-targeted ops
        var effectiveSchema = schema ?? AttributeSchema.Empty;
        var attributes = new AttributeValueCollection(effectiveSchema);
        foreach (var attr in profile.Attributes.Values)
        {
            if (effectiveSchema.AttributeDefinitions.ContainsKey(attr.Code))
            {
                attributes.Set(attr);
            }
        }

        string? passwordToSet = null;
        var removePassword = false;

        foreach (var op in body.Operations)
        {
            // Intercept password-targeted ops
            if (op.Path is not null &&
                op.Path.Equals(ScimConstants.Attributes.Password, StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable CA1308
                var opLower = op.Op?.ToLowerInvariant();
#pragma warning restore CA1308
                if (opLower is ScimConstants.PatchOps.Add or ScimConstants.PatchOps.Replace)
                {
                    if (op.Value is { ValueKind: JsonValueKind.String } valElem)
                    {
                        passwordToSet = valElem.GetString();
                        removePassword = false;
                    }
                    else
                    {
                        return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "password must be a string value");
                    }
                }
                else if (opLower == ScimConstants.PatchOps.Remove)
                {
                    removePassword = true;
                    passwordToSet = null;
                }
                else
                {
                    return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                        $"Unsupported operation '{op.Op}' for password attribute.");
                }

                // Skip profile attribute processing for password ops
                continue;
            }

            // Intercept no-path ops whose value object contains "password"
            if (op.Path is null && op.Value is { ValueKind: JsonValueKind.Object } valueObj)
            {
#pragma warning disable CA1308
                var noPathOpLower = op.Op?.ToLowerInvariant();
#pragma warning restore CA1308
                var hasPassword = false;
                string? extractedPassword = null;

                foreach (var prop in valueObj.EnumerateObject())
                {
                    if (prop.Name.Equals(ScimConstants.Attributes.Password, StringComparison.OrdinalIgnoreCase))
                    {
                        hasPassword = true;
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            extractedPassword = prop.Value.GetString();
                        }
                        else
                        {
                            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, "password must be a string value");
                        }

                        break;
                    }
                }

                if (hasPassword)
                {
                    if (noPathOpLower is not (ScimConstants.PatchOps.Add or ScimConstants.PatchOps.Replace))
                    {
                        return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                            $"Unsupported operation '{op.Op}' for password attribute without a path.");
                    }

                    passwordToSet = extractedPassword;
                    removePassword = false;

                    // Rebuild value object without "password" key and apply to profile attributes
                    var filteredProps = valueObj.EnumerateObject()
                        .Where(p => !p.Name.Equals(ScimConstants.Attributes.Password, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (filteredProps.Count > 0)
                    {
                        // Build a synthetic op without the password key by applying each remaining property
                        foreach (var prop in filteredProps)
                        {
                            var attrResult = ApplyAttributeValue(prop.Name, prop.Value, attributes, schema);
                            if (!attrResult.Success)
                            {
                                return attrResult.Error!;
                            }
                        }
                    }

                    continue;
                }
            }

            var applyResult = ApplyOperation(op, attributes, schema);
            if (!applyResult.Success)
            {
                return applyResult.Error!;
            }
        }

        // Validate password change requires authentication enabled
        if ((passwordToSet is not null || removePassword) && !IsAuthenticationEnabled)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "password is not supported when user authentication is not enabled.");
        }

        var validatedAttributes = attributes.Validate();
        var currentSchemaForPatch = effectiveSchema;
        if (!SchemaFreshnessCheck.IsValid(validatedAttributes, currentSchemaForPatch, logger))
        {
            return ScimOperationResult.Error(409, "Schema version mismatch. Please retry with the current schema.");
        }

        profile.ReplaceAttributes(validatedAttributes);

        // Build profile aspect operation
        var profileCurrentVersion = profileExistingResult.Value.Version;
        var profileAspectOp = await profileRepo.UpdateAspectOnlyBatchOperationAsync(profile, profileCurrentVersion, ct);
        var newProfileVersion = profileCurrentVersion + 1;
        var profileAspectRef = UserProfileRepository.GetAspectRef(profile, newProfileVersion);

        // Collect aspect refs starting with profile
        List<UserDso.AspectRef> aspectReferences = [profileAspectRef];
        List<IStoreOperation> operations = [];

        // Handle password changes in the same batch
        if (IsAuthenticationEnabled && (passwordToSet is not null || removePassword))
        {
            if (authenticatorsRepo is null)
            {
                throw new InvalidOperationException("Authenticators repo is not available but authentication is enabled.");
            }

            if (removePassword)
            {
                if (authExists && authenticators is not null && authenticators.RemovePassword())
                {
                    operations.Add(authenticatorsRepo.UpdateAspectOnlyBatchOperation(authenticators, authCurrentVersion));
                    aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(authenticators, authCurrentVersion + 1));
                }
                // If no authenticators exist or no password was set, nothing to remove — silently succeed
            }
            else if (passwordToSet is not null)
            {
                if (authExists && authenticators is not null)
                {
                    var (pwError, updatedAuth) = await TryApplyPasswordAsync(subjectId, passwordToSet, authenticators, ct);
                    if (pwError is not null)
                    {
                        return pwError;
                    }

                    operations.Add(authenticatorsRepo.UpdateAspectOnlyBatchOperation(updatedAuth, authCurrentVersion));
                    aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(updatedAuth, authCurrentVersion + 1));
                }
                else
                {
                    // Create new authenticators with the password in the same batch
                    var newAuth = new UserAuthenticators(subjectId, [], []);
                    var (pwError, updatedAuth) = await TryApplyPasswordAsync(subjectId, passwordToSet, newAuth, ct);
                    if (pwError is not null)
                    {
                        return pwError;
                    }

                    operations.Add(authenticatorsRepo.CreateAspectBatchOperation(updatedAuth));
                    aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(updatedAuth));
                }
            }
        }

        // Build UserDso update operation incorporating all aspect refs
        IStoreOperation userOp;
        int newUserDsoVersion;
        if (userExistingResult is not null)
        {
            var (existingUserDso, userVersion) = userExistingResult.Value;
            var updatedUserDso = existingUserDso;
            foreach (var aspectRef in aspectReferences)
            {
                updatedUserDso = UserRepository.AddOrUpdateAspectRef(updatedUserDso, aspectRef);
            }

            userOp = UserRepository.UpdateBatchOperation(updatedUserDso, userVersion);
            newUserDsoVersion = userVersion + 1;
        }
        else
        {
            userOp = UserRepository.CreateBatchOperation(subjectId, aspectReferences);
            newUserDsoVersion = 1;
        }

        // Final ordered list: userOp first, then auth ops (already in operations), then profile aspect last
        var orderedOperations = new List<IStoreOperation> { userOp };
        orderedOperations.AddRange(operations);
        orderedOperations.Add(profileAspectOp);

        var batchResult = await (await _storeFactory.GetStore(ct)).ExecuteBatchAsync(orderedOperations, [], ct);
        if (!batchResult.Success)
        {
            var mapError = MapBatchError(batchResult);
            if (mapError.StatusCode == 409)
            {
                return mapError;
            }

            logger.ScimUpdateUserRepositoryFailed(LogLevel.Warning, id);
            return mapError;
        }

        // Version is deterministic — no re-read needed
        var resource = ScimUserMapper.MapToResource(profile, newUserDsoVersion, serverUrls.BaseUrl, scimOptions.Value.Route);
        logger.ScimPatchUserSucceeded(LogLevel.Information, id);
        return ScimOperationResult.Ok(resource, ((ScimETag)newUserDsoVersion).ToHeaderValue(), id);
    }

    /// <summary>
    /// Validates and applies a raw password string to the given authenticators,
    /// using set vs. reset semantics depending on whether a password already exists.
    /// </summary>
    private async Task<(ScimOperationResult? error, UserAuthenticators updated)> TryApplyPasswordAsync(
        UserSubjectId subjectId,
        string rawPassword,
        UserAuthenticators authenticators,
        Ct ct)
    {
        if (passwordFactory is null)
        {
            throw new InvalidOperationException("PasswordFactory is not available but authentication is enabled.");
        }

        var passwordResult = await passwordFactory.CreateAsync(subjectId, rawPassword, ct);
        if (passwordResult is not PasswordCreationResult.Success { Password: var plainTextPassword })
        {
            var detail = passwordResult is PasswordCreationResult.Failed { Errors: var errors }
                ? string.Join(" ", errors)
                : "The provided password does not meet requirements.";
            return (ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue, detail), authenticators);
        }

        if (authenticators.HashedPassword is null)
        {
            var setSucceeded = authenticators.TrySetPassword(
                plainTextPassword,
                passwordHashAlgorithms?.Preferred
                    ?? throw new InvalidOperationException("PasswordHashAlgorithms is not registered."),
                timeProvider);
            if (!setSucceeded)
            {
                throw new InvalidOperationException($"TrySetPassword failed unexpectedly for user '{subjectId}'.");
            }
        }
        else
        {
            // SCIM acts as an external admin — password history policy does not apply.
            // Pass historyCount: 0 so the reset always succeeds regardless of policy.
            var resetSucceeded = authenticators.TryResetPassword(
                plainTextPassword,
                passwordHashAlgorithms?.Preferred
                    ?? throw new InvalidOperationException("PasswordHashAlgorithms is not registered."),
                passwordHashAlgorithms.All,
                historyCount: 0,
                timeProvider);
            if (!resetSucceeded)
            {
                throw new InvalidOperationException("TryResetPassword returned false with historyCount: 0 — this should never happen.");
            }
        }

        return (null, authenticators);
    }

    /// <summary>
    /// Maps the first failed operation in a <see cref="BatchResult"/> to a <see cref="ScimOperationResult"/>.
    /// </summary>
    private static ScimOperationResult MapBatchError(BatchResult result)
    {
        var firstFailure = result.Results.FirstOrDefault(r => r.Outcome is not OperationOutcome.Success)?.Outcome;
        return firstFailure switch
        {
            OperationOutcome.KeyConflict or OperationOutcome.AlreadyExists =>
                ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                    "A user with the same unique attribute value already exists."),
            OperationOutcome.DoesNotExist =>
                ScimOperationResult.Error(404, "User not found."),
            OperationOutcome.UnexpectedVersion =>
                ScimOperationResult.Error(412, "Precondition failed: ETag mismatch."),
            _ =>
                ScimOperationResult.Error(500, "An unexpected error occurred while updating the user.")
        };
    }

    private async Task<Result<UserProfile, ScimOperationResult>> TryCreateUserAsync(ScimUserRequest body, Ct ct)
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

        var subjectId = UserSubjectId.New();
        var profile = new UserProfile(subjectId, mapping.Attributes!);

        List<IStoreOperation> operations = [];
        List<UserDso.AspectRef> aspectReferences = [];

        var (profileAspectOp, profileAspectRef) = await profileRepo.CreateAspectBatchOperationAsync(profile, ct);
        aspectReferences.Add(profileAspectRef);

        if (IsAuthenticationEnabled && mapping.Password != null)
        {
            if (authenticatorsRepo is null)
            {
                throw new InvalidOperationException("Authenticators repo is not available but authentication is enabled.");
            }

            var authenticators = new UserAuthenticators(subjectId, [], []);
            var (pwError, updatedAuth) = await TryApplyPasswordAsync(subjectId, mapping.Password, authenticators, ct);
            if (pwError is not null)
            {
                return Result.Create(pwError);
            }

            var authAspectOp = authenticatorsRepo.CreateAspectBatchOperation(updatedAuth);
            aspectReferences.Add(UserAuthenticatorsRepository.GetAspectRef(updatedAuth));
            operations.Add(authAspectOp);
        }

        // Final ordered list: userOp first, then auth ops (already in operations), then profile aspect last
        var orderedOps = new List<IStoreOperation> { UserRepository.CreateBatchOperation(subjectId, aspectReferences) };
        orderedOps.AddRange(operations);
        orderedOps.Add(profileAspectOp);

        var batchResult = await (await _storeFactory.GetStore(ct)).ExecuteBatchAsync(orderedOps, [], ct);
        if (batchResult.Success)
        {
            licenseValidator.ValidateUserCount();
            return profile;
        }

        var createMapError = MapBatchError(batchResult);
        if (createMapError.StatusCode != 409)
        {
            logger.ScimCreateUserRepositoryFailed(LogLevel.Warning);
        }

        return Result.Create(createMapError);
    }

    private sealed class ApplyResult
    {
        internal static readonly ApplyResult Ok = new() { Success = true };
        internal bool Success { get; private init; }
        internal ScimOperationResult? Error { get; private init; }
        internal static ApplyResult FromError(ScimOperationResult error) => new() { Success = false, Error = error };
    }

    private static ApplyResult ApplyOperation(
        ScimPatchOperation op,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
#pragma warning disable CA1308
        var opLower = op.Op?.ToLowerInvariant();
#pragma warning restore CA1308
        return opLower switch
        {
            ScimConstants.PatchOps.Add => ApplyAdd(op, attributes, schema),
            ScimConstants.PatchOps.Replace => ApplyReplace(op, attributes, schema),
            ScimConstants.PatchOps.Remove => ApplyRemove(op, attributes, schema),
            _ => ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Unsupported operation '{op.Op}'. Supported: {ScimConstants.PatchOps.Add}, {ScimConstants.PatchOps.Replace}, {ScimConstants.PatchOps.Remove}."))
        };
    }

    private static ApplyResult ApplyAdd(
        ScimPatchOperation op,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Path is null)
        {
            return ApplyValueObjectKeys(op, attributes, schema);
        }

        var valueError = RequireValue(op);
        if (!valueError.Success)
        {
            return valueError;
        }

        var existingComplexSnapshot = SnapshotComplexAttribute(op.Path, attributes, schema);
        var setResult = ApplyAttributeValue(op.Path, op.Value!.Value, attributes, schema);
        if (!setResult.Success)
        {
            return setResult;
        }

        return existingComplexSnapshot is not null
            ? MergeComplexAttribute(op.Path, existingComplexSnapshot, attributes, schema!)
            : ApplyResult.Ok;
    }

    private static ApplyResult ApplyReplace(
        ScimPatchOperation op,
        AttributeValueCollection attributes,
        AttributeSchema? schema)
    {
        if (op.Path is null)
        {
            return ApplyValueObjectKeys(op, attributes, schema);
        }

        var valueError = RequireValue(op);
        if (!valueError.Success)
        {
            return valueError;
        }

        return ApplyAttributeValue(op.Path, op.Value!.Value, attributes, schema);
    }

    private static ApplyResult ApplyValueObjectKeys(
        ScimPatchOperation op,
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
            var attrResult = ApplyAttributeValue(prop.Name, prop.Value, attributes, schema);
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

    private static ApplyResult ApplyRemove(
        ScimPatchOperation op,
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
            if (AttributeCode.TryCreate(ScimConstants.UserNameAttributeName, out var userName))
            {
                _ = attributes.Remove(userName);
            }

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

    private static ApplyResult ApplyAttributeValue(
        string path,
        JsonElement value,
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
                if (schema is null ||
                    !AttributeCode.TryCreate(ScimConstants.UserNameAttributeName, out var userNameCode) ||
                    !schema.AttributeDefinitions.ContainsKey(userNameCode))
                {
                    return ApplyResult.Ok;
                }

                _ = attributes.Remove(userNameCode);
                return ApplyResult.Ok;
            }

            if (schema is null)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "userName attribute is not available without a registered schema."));
            }

            if (!AttributeCode.TryCreate(ScimConstants.UserNameAttributeName, out var userName))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "userName attribute name is invalid."));
            }

            if (!schema.AttributeDefinitions.TryGetValue(userName, out var userNameDef))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
                    "userName is not defined in the schema."));
            }

            if (!userNameDef.IsUnique)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "userName attribute must be configured as unique."));
            }

            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "userName must be a non-empty string."));
            }

            attributes.Set(userName, value.GetString()!);
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
