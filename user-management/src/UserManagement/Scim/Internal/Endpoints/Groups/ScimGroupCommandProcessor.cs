// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Duende.Storage;
using Duende.Storage.Internal.Filtering;
using Duende.Storage.Internal.Filtering.Expressions;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Membership.Internal.Storage;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Group = Duende.UserManagement.Membership.Group;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Groups;

internal sealed class ScimGroupCommandProcessor(
    IGroupAdmin groupAdmin,
    IMembershipAdmin membershipAdmin,
    GroupRepository groupRepository,
    MembershipRepository membershipRepository,
    IServerUrls serverUrls,
    IOptions<ScimEndpointOptions> scimOptions,
    IOptions<ScimOptions> options,
    ILogger<ScimGroupCommandProcessor> logger)
{
    internal async Task<ScimOperationResult> CreateAsync(ScimGroupRequest? body, Ct ct)
    {
        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        if (body.Schemas is not null &&
            !body.Schemas.Contains(ScimConstants.GroupSchemaUrn, StringComparer.OrdinalIgnoreCase))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax,
                $"Schemas must include '{ScimConstants.GroupSchemaUrn}'.");
        }

        var mapping = ScimGroupRequestMapper.Map(body);
        if (!mapping.IsSuccess)
        {
            return ScimOperationResult.Error(400, mapping.ErrorScimType, mapping.ErrorDetail);
        }

        var parseMembersResult = ParseMemberSubjectIds(mapping.MemberIds);
        if (!parseMembersResult.Success)
        {
            return parseMembersResult.Error!;
        }

        var memberSubjectIds = parseMembersResult.Value!;
        if (memberSubjectIds.Count > 0)
        {
            return await CreateWithMembersAsync(mapping, memberSubjectIds, ct);
        }

        var dto = new Group { Name = mapping.GroupName!.Value };
        var createResult = await groupAdmin.CreateAsync(dto, ct);
        if (!createResult.IsSuccess)
        {
            return MapSaveErrors(createResult.Errors!);
        }

        return BuildCreatedResponse(
            createResult.Id!.Value,
            mapping.GroupName!.Value,
            createResult.Version!.Value,
            mapping.MemberIds);
    }

    internal async Task<ScimOperationResult> ReplaceAsync(string id, ScimGroupRequest? body, string? ifMatch, Ct ct)
    {
        if (!GroupId.TryCreate(id, out var parsedGroupId))
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        var groupId = parsedGroupId;
        var existing = await groupAdmin.GetAsync(groupId, ct);
        if (!existing.Found)
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var currentVersion = existing.Version!.Value;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, currentVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var mapping = ScimGroupRequestMapper.Map(body);
        if (!mapping.IsSuccess)
        {
            return ScimOperationResult.Error(400, mapping.ErrorScimType, mapping.ErrorDetail);
        }

        var currentIds = await ScimGroupMemberHelper.GetAllMemberIdsAsync(membershipAdmin, groupId, ct);
        var parseRequestedResult = ParseRequestedMemberIds(mapping.MemberIds);
        if (!parseRequestedResult.Success)
        {
            return parseRequestedResult.Error!;
        }

        var requestedSubjectIds = parseRequestedResult.Value!;
        var toAdd = requestedSubjectIds.Where(r => !currentIds.Contains(r)).ToList();
        var toRemove = currentIds.Where(c => !requestedSubjectIds.Contains(c)).ToList();

        List<UuidV7>? addUuids = null;
        if (toAdd.Count > 0)
        {
            var resolveResult = await ResolveAndValidateMemberUuidsAsync(toAdd, ct);
            if (!resolveResult.Success)
            {
                return resolveResult.Error!;
            }

            addUuids = resolveResult.Value!;
        }

        List<UuidV7>? removeUuids = null;
        if (toRemove.Count > 0)
        {
            var (resolved, _) = await membershipRepository.ResolveUserUuidsAsync(toRemove, ct);
            removeUuids = resolved.Values.ToList();
        }

        var existingGroup = await groupRepository.TryReadAsync(groupId, ct);
        if (!existingGroup.HasValue)
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var (group, _) = existingGroup.Value;
        group.SetName(mapping.GroupName!.Value);

        var batchResult = await groupRepository.UpdateWithMembershipChangesAsync(
            group, currentVersion, addUuids ?? [], removeUuids ?? [], ct);

        if (!batchResult.Success)
        {
            return MapBatchErrors(batchResult);
        }

        var newVersion = currentVersion + 1;
        var resource = await BuildResourceAsync(groupId, mapping.GroupName!.Value, newVersion, ct);
        return ScimOperationResult.Ok(resource, ((ScimETag)newVersion).ToHeaderValue(), groupId.Value);
    }

    internal async Task<ScimOperationResult> PatchAsync(string id, ScimPatchRequest? body, string? ifMatch, Ct ct)
    {
        if (!GroupId.TryCreate(id, out var parsedGroupId))
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        if (body is null)
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        if (body.Operations is not { Count: > 0 })
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "At least one operation is required.");
        }

        if (body.Schemas is not null &&
            !body.Schemas.Contains(ScimConstants.PatchOpSchemaUrn, StringComparer.OrdinalIgnoreCase))
        {
            return ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidSyntax,
                $"Schemas must include '{ScimConstants.PatchOpSchemaUrn}'.");
        }

        var groupId = parsedGroupId;
        var existing = await groupAdmin.GetAsync(groupId, ct);
        if (!existing.Found)
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var currentVersion = existing.Version!.Value;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, currentVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var patchState = new PatchState(existing.Item!.Name);
        foreach (var op in body.Operations)
        {
            var applyResult = await ApplyOperationAsync(op, groupId, patchState, ct);
            if (!applyResult.Success)
            {
                return applyResult.Error!;
            }
        }

        var nameChanged = patchState.CurrentName != existing.Item.Name;
        var hasMembershipChanges = patchState.MembersToAdd.Count > 0 || patchState.MembersToRemove.Count > 0;

        if (nameChanged || hasMembershipChanges)
        {
            var persistError = await PersistChangesAtomicallyAsync(
                groupId, existing.Item, patchState, currentVersion, ct);
            if (persistError is not null)
            {
                return persistError;
            }

            currentVersion++;
        }

        var resource = await BuildResourceAsync(groupId, patchState.CurrentName, currentVersion, ct);
        return ScimOperationResult.Ok(resource, ((ScimETag)currentVersion).ToHeaderValue(), groupId.Value);
    }

    internal async Task<ScimOperationResult> DeleteAsync(string id, string? ifMatch, Ct ct)
    {
        if (!GroupId.TryCreate(id, out var parsedGroupId))
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var groupId = parsedGroupId;
        var existing = await groupAdmin.GetAsync(groupId, ct);
        if (!existing.Found)
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var currentVersion = existing.Version!.Value;
        var preconditionError = ScimEndpointHelpers.CheckIfMatchResult(ifMatch, currentVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var deleteResult = await groupAdmin.DeleteAsync(groupId, ct);
        if (!deleteResult.IsSuccess)
        {
            var first = deleteResult.Errors![0];
            return first.Code switch
            {
                "not_found" => ScimOperationResult.Error(404, "Group not found."),
                _ => ScimOperationResult.Error(500, "An unexpected error occurred while deleting the group.")
            };
        }

        return ScimOperationResult.NoContent();
    }

    private async Task<ScimOperationResult> CreateWithMembersAsync(
        ScimGroupRequestMapper.MappingResult mapping,
        List<UserSubjectId> memberSubjectIds,
        Ct ct)
    {
        var resolveResult = await ResolveAndValidateMemberUuidsAsync(memberSubjectIds, ct);
        if (!resolveResult.Success)
        {
            return resolveResult.Error!;
        }

        var group = Membership.Internal.Group.Create(mapping.GroupName!.Value);
        var batchResult = await groupRepository.CreateWithMembersAsync(group, resolveResult.Value!, ct);
        if (!batchResult.Success)
        {
            return MapCreateBatchErrors(batchResult, mapping.GroupName!.Value);
        }

        return BuildCreatedResponse(group.Id, mapping.GroupName!.Value, 1, mapping.MemberIds);
    }

    private async Task<ScimOperationResult?> PersistChangesAtomicallyAsync(
        GroupId groupId,
        Group current,
        PatchState patchState,
        int currentVersion,
        Ct ct)
    {
        List<UuidV7>? addUuids = null;
        if (patchState.MembersToAdd.Count > 0)
        {
            var addSubjectIds = patchState.MembersToAdd.ToList();
            var resolveResult = await ResolveAndValidateMemberUuidsAsync(addSubjectIds, ct);
            if (!resolveResult.Success)
            {
                return resolveResult.Error;
            }

            addUuids = resolveResult.Value;
        }

        List<UuidV7>? removeUuids = null;
        if (patchState.MembersToRemove.Count > 0)
        {
            var removeSubjectIds = patchState.MembersToRemove.ToList();
            var (resolved, _) = await membershipRepository.ResolveUserUuidsAsync(removeSubjectIds, ct);
            removeUuids = resolved.Values.ToList();
        }

        var existingGroup = await groupRepository.TryReadAsync(groupId, ct);
        if (!existingGroup.HasValue)
        {
            return ScimOperationResult.Error(404, "Group not found.");
        }

        var (group, _) = existingGroup.Value;
        group.SetName(patchState.CurrentName);
        group.SetDescription(current.Description);

        var batchResult = await groupRepository.UpdateWithMembershipChangesAsync(
            group, currentVersion, addUuids ?? [], removeUuids ?? [], ct);

        return batchResult.Success ? null : MapBatchErrors(batchResult);
    }

    private async Task<ScimGroupResource> BuildResourceAsync(GroupId groupId, GroupName groupName, int version, Ct ct)
    {
        var members = await ScimGroupMemberHelper.FetchMembersWithTruncationAsync(
            membershipAdmin,
            groupId,
            options.Value.MaxGroupMembersInResponse,
            serverUrls.BaseUrl,
            scimOptions.Value.Route,
            logger,
            ct);

        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = groupName
        };

        return ScimGroupMapper.MapToResource(
            groupListDto,
            members.Count > 0 ? members : null,
            version,
            serverUrls.BaseUrl,
            scimOptions.Value.GroupsRoute);
    }

    private ScimOperationResult BuildCreatedResponse(
        GroupId groupId,
        GroupName groupName,
        int version,
        IReadOnlyList<string>? memberIds)
    {
        List<ScimGroupMember>? members = null;
        if (memberIds is { Count: > 0 })
        {
            members = memberIds
                .Select(id => new ScimGroupMember
                {
                    Value = id,
                    Ref = $"{serverUrls.BaseUrl}{scimOptions.Value.Route}/{id}",
                    Type = ScimConstants.ResourceTypes.User
                })
                .ToList();
        }

        var groupListDto = new GroupListItem
        {
            Id = groupId,
            Name = groupName
        };

        var resource = ScimGroupMapper.MapToResource(
            groupListDto,
            members,
            version,
            serverUrls.BaseUrl,
            scimOptions.Value.GroupsRoute);

        return ScimOperationResult.Created(
            resource,
            resource.Meta.Location,
            ((ScimETag)version).ToHeaderValue(),
            groupId.Value);
    }

    private static Result<List<UserSubjectId>, ScimOperationResult> ParseMemberSubjectIds(IReadOnlyList<string>? memberIds)
    {
        if (memberIds is not { Count: > 0 })
        {
            return new List<UserSubjectId>();
        }

        var subjectIds = new List<UserSubjectId>(memberIds.Count);
        var invalid = new List<string>();
        foreach (var memberId in memberIds)
        {
            if (Guid.TryParse(memberId, out var memberGuid))
            {
                subjectIds.Add(UserSubjectId.Create(memberGuid.ToString()));
            }
            else
            {
                invalid.Add(memberId);
            }
        }

        return invalid.Count > 0
            ? Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid member value(s): {string.Join(", ", invalid)}. Member values must be valid user IDs (GUIDs)."))
            : subjectIds;
    }

    private async Task<Result<List<UuidV7>, ScimOperationResult>> ResolveAndValidateMemberUuidsAsync(
        List<UserSubjectId> subjectIds,
        Ct ct)
    {
        var resolved = new Dictionary<UserSubjectId, UuidV7>(subjectIds.Count);
        foreach (var subjectId in subjectIds)
        {
            var userUuid = await membershipRepository.GetOrCreateUserUuidAsync(subjectId, ct);
            resolved[subjectId] = userUuid;
        }

        return resolved.Values.ToList();
    }

    private static Result<HashSet<UserSubjectId>, ScimOperationResult> ParseRequestedMemberIds(IReadOnlyList<string>? memberIds)
    {
        var requestedIds = new HashSet<UserSubjectId>();
        if (memberIds is null)
        {
            return requestedIds;
        }

        var invalid = new List<string>();
        foreach (var memberId in memberIds)
        {
            if (Guid.TryParse(memberId, out var guid))
            {
                _ = requestedIds.Add(UserSubjectId.Create(guid.ToString()));
            }
            else
            {
                invalid.Add(memberId);
            }
        }

        return invalid.Count > 0
            ? Result.Create(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid member value(s): {string.Join(", ", invalid)}. Member values must be valid user IDs (GUIDs)."))
            : requestedIds;
    }

    private sealed class PatchState(GroupName initialName)
    {
        internal GroupName CurrentName { get; set; } = initialName;
        private HashSet<UserSubjectId>? _effectiveMembers;
        internal HashSet<UserSubjectId> MembersToAdd { get; } = [];
        internal HashSet<UserSubjectId> MembersToRemove { get; } = [];
        internal bool MembersLoaded => _effectiveMembers is not null;

        internal void LoadCurrentMembers(HashSet<UserSubjectId> currentMembers)
        {
            _effectiveMembers = currentMembers;
            foreach (var toAdd in MembersToAdd)
            {
                _ = _effectiveMembers.Add(toAdd);
            }

            foreach (var toRemove in MembersToRemove)
            {
                _ = _effectiveMembers.Remove(toRemove);
            }
        }

        internal void AddMember(UserSubjectId subjectId)
        {
            if (_effectiveMembers is not null && !_effectiveMembers.Add(subjectId))
            {
                return;
            }

            if (!MembersToRemove.Remove(subjectId))
            {
                _ = MembersToAdd.Add(subjectId);
            }
        }

        internal void RemoveMember(UserSubjectId subjectId)
        {
            if (_effectiveMembers is not null && !_effectiveMembers.Remove(subjectId))
            {
                return;
            }

            if (!MembersToAdd.Remove(subjectId))
            {
                _ = MembersToRemove.Add(subjectId);
            }
        }

        internal void ReplaceMembers(HashSet<UserSubjectId> requestedIds)
        {
            MembersToAdd.Clear();
            MembersToRemove.Clear();

            foreach (var current in _effectiveMembers!)
            {
                if (!requestedIds.Contains(current))
                {
                    _ = MembersToRemove.Add(current);
                }
            }

            foreach (var requested in requestedIds)
            {
                if (!_effectiveMembers.Contains(requested))
                {
                    _ = MembersToAdd.Add(requested);
                }
            }

            _effectiveMembers = requestedIds;
        }

        internal void RemoveAllMembers()
        {
            MembersToAdd.Clear();
            MembersToRemove.Clear();

            foreach (var current in _effectiveMembers!)
            {
                _ = MembersToRemove.Add(current);
            }

            _effectiveMembers.Clear();
        }
    }

    private sealed class ApplyResult
    {
        internal static readonly ApplyResult Ok = new() { Success = true };

        [MemberNotNullWhen(false, nameof(Error))]
        internal bool Success { get; private init; }

        internal ScimOperationResult? Error { get; private init; }

        internal static ApplyResult FromError(ScimOperationResult error) =>
            new() { Success = false, Error = error };
    }

    private async Task<ApplyResult> ApplyOperationAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
#pragma warning disable CA1308
        var opLower = op.Op?.ToLowerInvariant();
#pragma warning restore CA1308

        return opLower switch
        {
            ScimConstants.PatchOps.Add => await ApplyAddAsync(op, groupId, state, ct),
            ScimConstants.PatchOps.Replace => await ApplyReplaceAsync(op, groupId, state, ct),
            ScimConstants.PatchOps.Remove => await ApplyRemoveAsync(op, groupId, state, ct),
            _ => ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Unsupported operation '{op.Op}'. Supported: add, replace, remove."))
        };
    }

    private async Task<ApplyResult> ApplyAddAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        if (string.Equals(op.Path, ScimConstants.Attributes.Members, StringComparison.OrdinalIgnoreCase))
        {
            return AddMembers(op, state);
        }

        if (string.Equals(op.Path, ScimConstants.Attributes.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return ApplyDisplayNameChange(op, state);
        }

        if (op.Path is null && op.Value is { ValueKind: JsonValueKind.Object } valueObj)
        {
            return await ApplyObjectKeysAsync(valueObj, ScimConstants.PatchOps.Add, groupId, state, ct);
        }

        return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
            $"Unsupported path '{op.Path}' for add operation on Group."));
    }

    private async Task<ApplyResult> ApplyReplaceAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        if (string.Equals(op.Path, ScimConstants.Attributes.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return ApplyDisplayNameChange(op, state);
        }

        if (string.Equals(op.Path, ScimConstants.Attributes.Members, StringComparison.OrdinalIgnoreCase))
        {
            return await ReplaceMembersAsync(op, groupId, state, ct);
        }

        if (op.Path is null && op.Value is { ValueKind: JsonValueKind.Object } valueObj)
        {
            return await ApplyObjectKeysAsync(valueObj, ScimConstants.PatchOps.Replace, groupId, state, ct);
        }

        return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
            $"Unsupported path '{op.Path}' for replace operation on Group."));
    }

    private async Task<ApplyResult> ApplyRemoveAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        if (op.Path is null)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.NoTarget,
                "Operation 'remove' requires a 'path'."));
        }

        if (string.Equals(op.Path, ScimConstants.Attributes.Members, StringComparison.OrdinalIgnoreCase))
        {
            return await RemoveMembersAsync(op, groupId, state, ct);
        }

        if (TryParseMemberValueFilter(op.Path, out var memberId))
        {
            if (!Guid.TryParse(memberId, out var memberGuid))
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid member value: {memberId}. Member values must be valid user IDs (GUIDs)."));
            }

            state.RemoveMember(UserSubjectId.Create(memberGuid.ToString()));
            return ApplyResult.Ok;
        }

        return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidPath,
            $"Unsupported path '{op.Path}' for remove operation on Group."));
    }

    private async Task<ApplyResult> ApplyObjectKeysAsync(
        JsonElement valueObj,
        string enclosingOp,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        foreach (var prop in valueObj.EnumerateObject())
        {
            if (string.Equals(prop.Name, ScimConstants.Attributes.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                var fakeOp = new ScimPatchOperation { Op = ScimConstants.PatchOps.Replace, Path = prop.Name, Value = prop.Value };
                var nameResult = ApplyDisplayNameChange(fakeOp, state);
                if (!nameResult.Success)
                {
                    return nameResult;
                }
            }
            else if (string.Equals(prop.Name, ScimConstants.Attributes.Members, StringComparison.OrdinalIgnoreCase))
            {
                var fakeOp = new ScimPatchOperation { Op = enclosingOp, Path = prop.Name, Value = prop.Value };
                var membersResult = string.Equals(enclosingOp, ScimConstants.PatchOps.Replace, StringComparison.OrdinalIgnoreCase)
                    ? await ReplaceMembersAsync(fakeOp, groupId, state, ct)
                    : AddMembers(fakeOp, state);

                if (!membersResult.Success)
                {
                    return membersResult;
                }
            }
        }

        return ApplyResult.Ok;
    }

    private static ApplyResult ApplyDisplayNameChange(ScimPatchOperation op, PatchState state)
    {
        if (!op.Value.HasValue || op.Value.Value.ValueKind != JsonValueKind.String)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "displayName must be a string."));
        }

        var nameStr = op.Value.Value.GetString() ?? string.Empty;
        if (!GroupName.TryCreate(nameStr, out var groupName))
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid displayName value: '{nameStr}'."));
        }

        state.CurrentName = groupName.Value;
        return ApplyResult.Ok;
    }

    private static ApplyResult AddMembers(ScimPatchOperation op, PatchState state)
    {
        var memberIds = ExtractMemberIds(op.Value);
        if (memberIds is null)
        {
            return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                "members value must be an array of objects with a 'value' property."));
        }

        var invalid = new List<string>();
        foreach (var memberId in memberIds)
        {
            if (Guid.TryParse(memberId, out var memberGuid))
            {
                state.AddMember(UserSubjectId.Create(memberGuid.ToString()));
            }
            else
            {
                invalid.Add(memberId);
            }
        }

        return invalid.Count > 0
            ? ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                $"Invalid member value(s): {string.Join(", ", invalid)}. Member values must be valid user IDs (GUIDs)."))
            : ApplyResult.Ok;
    }

    private async Task<ApplyResult> RemoveMembersAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        if (op.Value.HasValue)
        {
            var memberIds = ExtractMemberIds(op.Value);
            if (memberIds is null)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "members value must be an array of objects with a 'value' property."));
            }

            var invalid = new List<string>();
            foreach (var memberId in memberIds)
            {
                if (Guid.TryParse(memberId, out var memberGuid))
                {
                    state.RemoveMember(UserSubjectId.Create(memberGuid.ToString()));
                }
                else
                {
                    invalid.Add(memberId);
                }
            }

            return invalid.Count > 0
                ? ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid member value(s): {string.Join(", ", invalid)}. Member values must be valid user IDs (GUIDs)."))
                : ApplyResult.Ok;
        }

        if (!state.MembersLoaded)
        {
            var currentIds = await ScimGroupMemberHelper.GetAllMemberIdsAsync(membershipAdmin, groupId, ct);
            state.LoadCurrentMembers(currentIds);
        }

        state.RemoveAllMembers();
        return ApplyResult.Ok;
    }

    private async Task<ApplyResult> ReplaceMembersAsync(
        ScimPatchOperation op,
        GroupId groupId,
        PatchState state,
        Ct ct)
    {
        var requestedIds = new HashSet<UserSubjectId>();
        if (op.Value.HasValue)
        {
            var memberIds = ExtractMemberIds(op.Value);
            if (memberIds is null)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    "members value must be an array of objects with a 'value' property."));
            }

            var invalid = new List<string>();
            foreach (var memberId in memberIds)
            {
                if (Guid.TryParse(memberId, out var memberGuid))
                {
                    _ = requestedIds.Add(UserSubjectId.Create(memberGuid.ToString()));
                }
                else
                {
                    invalid.Add(memberId);
                }
            }

            if (invalid.Count > 0)
            {
                return ApplyResult.FromError(ScimOperationResult.Error(400, ScimConstants.ErrorTypes.InvalidValue,
                    $"Invalid member value(s): {string.Join(", ", invalid)}. Member values must be valid user IDs (GUIDs)."));
            }
        }

        if (!state.MembersLoaded)
        {
            var currentIds = await ScimGroupMemberHelper.GetAllMemberIdsAsync(membershipAdmin, groupId, ct);
            state.LoadCurrentMembers(currentIds);
        }

        state.ReplaceMembers(requestedIds);
        return ApplyResult.Ok;
    }

    private static List<string>? ExtractMemberIds(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Array } arr)
        {
            return null;
        }

        var ids = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (item.TryGetProperty(ScimConstants.Attributes.Value, out var valueProp) &&
                valueProp.ValueKind == JsonValueKind.String)
            {
                var idStr = valueProp.GetString();
                if (!string.IsNullOrWhiteSpace(idStr))
                {
                    ids.Add(idStr);
                }
            }
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ScimOperationResult MapCreateBatchErrors(BatchResult batchResult, GroupName groupName)
    {
        var failedOp = batchResult.Results[^1];
        return failedOp.Outcome switch
        {
            OperationOutcome.AlreadyExists => ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                $"A Group with identifier '{groupName.Value}' already exists."),
            OperationOutcome.KeyConflict => ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                $"The value '{groupName.Value}' for Name is already in use."),
            _ => ScimOperationResult.Error(400, $"Batch operation failed at index {failedOp.Index}: {failedOp.Outcome}")
        };
    }

    private static ScimOperationResult MapSaveErrors(IReadOnlyList<AdminError> errors)
    {
        var first = errors[0];
        return first.Code switch
        {
            "already_exists" => ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness, first.Message),
            "duplicate_value" => ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness, first.Message),
            _ => ScimOperationResult.Error(400, first.Message)
        };
    }

    private static ScimOperationResult MapBatchErrors(BatchResult batchResult)
    {
        var failedOp = batchResult.Results[^1];
        return failedOp.Outcome switch
        {
            OperationOutcome.DoesNotExist => ScimOperationResult.Error(404, "Group not found."),
            OperationOutcome.UnexpectedVersion => ScimOperationResult.Error(412, "Precondition failed: ETag mismatch."),
            OperationOutcome.KeyConflict => ScimOperationResult.Error(409, ScimConstants.ErrorTypes.Uniqueness,
                "The group name is already in use."),
            _ => ScimOperationResult.Error(400, $"Batch operation failed at index {failedOp.Index}: {failedOp.Outcome}")
        };
    }

    private static bool TryParseMemberValueFilter(string path, [NotNullWhen(true)] out string? memberId)
    {
        memberId = null;

        if (!FilterExpressionParser.TryParse(path, out var expression))
        {
            return false;
        }

        if (expression is ComplexAttributeExpression
            {
                AttributePath.Path: var attrName,
                Filter: ComparisonExpression
                {
                    Operator: ComparisonOperator.Equal,
                    AttributePath.Path: var filterAttr,
                    Value: string filterValue
                }
            } &&
            attrName.Equals(ScimConstants.Attributes.Members, StringComparison.OrdinalIgnoreCase) &&
            filterAttr.Equals(ScimConstants.Attributes.Value, StringComparison.OrdinalIgnoreCase))
        {
            memberId = filterValue;
            return true;
        }

        return false;
    }
}
