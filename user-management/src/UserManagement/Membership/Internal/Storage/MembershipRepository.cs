// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Internal.Storage;

namespace Duende.UserManagement.Membership.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class MembershipRepository(UserRepository userRepository)
{
    /// <summary>
    /// Resolves a <see cref="UserSubjectId"/> to the UserDso UUID used as the left side of membership links.
    /// Creates the UserDso if it does not yet exist.
    /// </summary>
    internal async Task<UuidV7> GetOrCreateUserUuidAsync(UserSubjectId subjectId, Ct ct)
    {
        var existing = await userRepository.TryReadAsync(subjectId, ct);
        if (existing is var (user, _))
        {
            return UuidV7.From(user.Id);
        }

        // User doesn't exist yet — create a minimal UserDso
        var createResult = await userRepository.CreateAsync(subjectId, ct);

        // Handle race condition: another request may have created the user concurrently
        if (createResult is not CreateResult.Success)
        {
            existing = await userRepository.TryReadAsync(subjectId, ct);
            if (existing is var (raceUser, _))
            {
                return UuidV7.From(raceUser.Id);
            }
        }

        // Re-read to get the UUID of the just-created user
        var created = await userRepository.TryReadAsync(subjectId, ct);
        return created is var (createdUser, _)
            ? UuidV7.From(createdUser.Id)
            : throw new InvalidOperationException($"Failed to create or read UserDso for subject '{subjectId}'.");
    }

    /// <summary>
    /// Resolves a list of <see cref="UserSubjectId"/> values to their UserDso UUIDs.
    /// Returns a dictionary of resolved mappings and a list of subject IDs that could not be found.
    /// </summary>
    internal async Task<(Dictionary<UserSubjectId, UuidV7> Resolved, List<UserSubjectId> NotFound)>
        ResolveUserUuidsAsync(IReadOnlyList<UserSubjectId> subjectIds, Ct ct) =>
        await userRepository.ResolveUserUuidsAsync(subjectIds, ct);
}
