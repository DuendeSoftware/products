// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Duende.UserManagement.Admin;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Membership.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class RoleAdmin(RoleRepository roleRepository, ILogger<RoleAdmin> logger, UserManagementLicenseValidator licenseValidator) : IRoleAdmin
{
    public async Task<SaveResult<RoleId>> CreateAsync(Membership.Role dto, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        // Check if a role with this name already exists
        var existing = await roleRepository.TryReadAsync(dto.Name, ct);
        if (existing.HasValue)
        {
            logger.RoleCreateDuplicateName(LogLevel.Warning, dto.Name.Value);
            return AdminError.AlreadyExists("Role", dto.Name.Value, nameof(dto.Name));
        }

        // Create the domain entity
        var role = Role.Create(dto.Name);

        // Apply optional fields
        if (dto.Description is not null)
        {
            role.SetDescription(dto.Description);
        }

        // Persist
        var result = await roleRepository.CreateAsync(role, ct);

        if (result == CreateResult.Success)
        {
            logger.RoleCreateSucceeded(LogLevel.Information, role.Id);
            return SaveResult.Success(role.Id, 1);
        }

        return result switch
        {
            CreateResult.AlreadyExists => AdminError.AlreadyExists("Role", dto.Name.Value, nameof(dto.Name)),
            CreateResult.KeyConflict => AdminError.DuplicateValue(nameof(dto.Name), dto.Name.Value),
            _ => throw new InvalidOperationException($"Unknown value {result}")
        };
    }

    public async Task<GetResult<Membership.Role>> GetAsync(RoleId id, Ct ct)
    {
        var result = await roleRepository.TryReadAsync(id, ct);

        if (!result.HasValue)
        {
            logger.RoleNotFound(LogLevel.Warning, id);
            return new GetResult<Membership.Role>();
        }

        return GetResult.Found(ToDto(result.Value.Role), result.Value.Version);
    }

    public async Task<SaveResult<RoleId>> UpdateAsync(RoleId id, Membership.Role dto, Admin.DataVersion expectedVersion, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        // Load existing role
        var existing = await roleRepository.TryReadAsync(id, ct);
        if (!existing.HasValue)
        {
            logger.RoleNotFound(LogLevel.Warning, id);
            return AdminError.NotFound("Role", id.ToString());
        }

        var (role, currentVersion) = existing.Value;

        // Apply DTO to entity
        role.SetName(dto.Name);
        role.SetDescription(dto.Description);

        // Persist
        var result = await roleRepository.UpdateAsync(role, expectedVersion.Value, ct);

        if (result == UpdateResult.Success)
        {
            logger.RoleUpdateSucceeded(LogLevel.Information, id);
            return SaveResult.Success(role.Id, currentVersion + 1);
        }

        if (result == UpdateResult.UnexpectedVersion)
        {
            logger.RoleUpdateVersionConflict(LogLevel.Warning, id);
            return AdminError.VersionConflict();
        }

        return result switch
        {
            UpdateResult.DoesNotExist => AdminError.NotFound("Role", id.ToString()),
            UpdateResult.KeyConflict => AdminError.DuplicateValue(nameof(dto.Name), dto.Name.Value),
            _ => throw new InvalidOperationException($"Unknown value {result}")
        };
    }

    public async Task<SaveResult<RoleId>> DeleteAsync(RoleId id, Ct ct)
    {
        if (!licenseValidator.ValidateRolesAndGroups())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Roles and Groups feature.");
        }
        // Check existence
        var existing = await roleRepository.TryReadAsync(id, ct);
        if (!existing.HasValue)
        {
            logger.RoleDeleteNotFound(LogLevel.Warning, id);
            return AdminError.NotFound("Role", id.ToString());
        }

        // Delete via repository
        var result = await roleRepository.DeleteAsync(id, ct);

        if (result == DeleteResult.Success)
        {
            logger.RoleDeleteSucceeded(LogLevel.Information, id);
            return SaveResult.Success(id, 0);
        }

        throw new InvalidOperationException("Unexpected DeleteResult " + result);
    }

    public async Task<QueryResult<RoleListItem>> QueryAsync(QueryRequest<RoleFilter, RoleSortField> request, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await roleRepository.QueryAsync(request.Filter?.FilterValue, request.Sort, request.Range, ct);

        logger.RoleQueryExecuted(LogLevel.Debug);
        return result.ConvertTo(ToListDto);
    }

    private static Membership.Role ToDto(Role role) =>
        new()
        {
            Name = role.Name,
            Description = role.Description
        };

    private static RoleListItem ToListDto(Role role) =>
        new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description
        };
}
