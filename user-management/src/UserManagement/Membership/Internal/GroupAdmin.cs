// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Membership.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class GroupAdmin(GroupRepository groupRepository, ILogger<GroupAdmin> logger) : IGroupAdmin
{
    public async Task<SaveResult<GroupId>> CreateAsync(Membership.Group dto, Ct ct)
    {
        // Check if a group with this name already exists
        var existing = await groupRepository.TryReadAsync(dto.Name, ct);
        if (existing.HasValue)
        {
            logger.GroupCreateDuplicateName(LogLevel.Warning, dto.Name.Value);
            return AdminError.AlreadyExists("Group", dto.Name.Value, nameof(dto.Name));
        }

        // Create the domain entity
        var group = Group.Create(dto.Name);

        // Apply optional fields
        if (dto.Description is not null)
        {
            group.SetDescription(dto.Description);
        }

        // Persist
        var result = await groupRepository.CreateAsync(group, ct);

        if (result == CreateResult.Success)
        {
            logger.GroupCreateSucceeded(LogLevel.Information, group.Id);
            return SaveResult.Success(group.Id, 1);
        }

        return result switch
        {
            CreateResult.AlreadyExists => AdminError.AlreadyExists("Group", dto.Name.Value, nameof(dto.Name)),
            CreateResult.KeyConflict => AdminError.DuplicateValue(nameof(dto.Name), dto.Name.Value),
            _ => throw new InvalidOperationException($"Unknown value {result}")
        };
    }

    public async Task<GetResult<Membership.Group>> GetAsync(GroupId id, Ct ct)
    {
        var result = await groupRepository.TryReadAsync(id, ct);

        if (!result.HasValue)
        {
            logger.GroupNotFound(LogLevel.Warning, id);
            return new GetResult<Membership.Group>();
        }

        return GetResult.Found(ToDto(result.Value.Group), result.Value.Version);
    }

    public async Task<SaveResult<GroupId>> UpdateAsync(GroupId id, Membership.Group dto, Admin.DataVersion expectedVersion, Ct ct)
    {
        // Load existing group
        var existing = await groupRepository.TryReadAsync(id, ct);
        if (!existing.HasValue)
        {
            logger.GroupNotFound(LogLevel.Warning, id);
            return AdminError.NotFound("Group", id.ToString());
        }

        var (group, currentVersion) = existing.Value;

        // Apply DTO to entity
        group.SetName(dto.Name);
        group.SetDescription(dto.Description);

        // Persist
        var result = await groupRepository.UpdateAsync(group, expectedVersion.Value, ct);

        if (result == UpdateResult.Success)
        {
            logger.GroupUpdateSucceeded(LogLevel.Information, id);
            return SaveResult.Success(group.Id, currentVersion + 1);
        }

        if (result == UpdateResult.UnexpectedVersion)
        {
            logger.GroupUpdateVersionConflict(LogLevel.Warning, id);
            return AdminError.VersionConflict();
        }

        return result switch
        {
            UpdateResult.DoesNotExist => AdminError.NotFound("Group", id.ToString()),
            UpdateResult.KeyConflict => AdminError.DuplicateValue(nameof(dto.Name), dto.Name.Value),
            _ => throw new InvalidOperationException($"Unknown value {result}")
        };
    }

    public async Task<SaveResult<GroupId>> DeleteAsync(GroupId id, Ct ct)
    {
        // Check existence
        var existing = await groupRepository.TryReadAsync(id, ct);
        if (!existing.HasValue)
        {
            logger.GroupDeleteNotFound(LogLevel.Warning, id);
            return AdminError.NotFound("Group", id.ToString());
        }

        // Delete via repository
        var result = await groupRepository.DeleteAsync(id, ct);

        if (result == DeleteResult.Success)
        {
            logger.GroupDeleteSucceeded(LogLevel.Information, id);
            return SaveResult.Success(id, 0);
        }

        throw new InvalidOperationException("Unexpected DeleteResult " + result);
    }

    public async Task<QueryResult<GroupListItem>> QueryAsync(QueryRequest<GroupFilter, GroupSortField> request, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await groupRepository.QueryAsync(request.Filter?.FilterValue, request.Sort, request.Range, ct);

        logger.GroupQueryExecuted(LogLevel.Debug);
        return result.ConvertTo(ToListDto);
    }

    private static Membership.Group ToDto(Group group) =>
        new()
        {
            Name = group.Name,
            Description = group.Description
        };

    private static GroupListItem ToListDto(Group group) =>
        new()
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description
        };
}
