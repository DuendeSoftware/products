// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Internal;

namespace IdentityServer.UnitTests.Storage;

public class EntityTypeUniquenessTests
{
    private static readonly IReadOnlyList<EntityType> EntityTypes = DiscoverEntityTypes();

    [Fact]
    public void EntityType_ids_are_unique()
    {
        EntityTypes.ShouldNotBeEmpty();

        var duplicateIds = EntityTypes
            .GroupBy(et => et.Id)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate EntityType Id {g.Key}: {string.Join(", ", g.Select(et => et.Name))}")
            .ToList();

        duplicateIds.ShouldBeEmpty();
    }

    [Fact]
    public void EntityType_names_are_unique()
    {
        EntityTypes.ShouldNotBeEmpty();

        var duplicateNames = EntityTypes
            .GroupBy(et => et.Name, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate EntityType Name '{g.Key}': IDs {string.Join(", ", g.Select(et => et.Id))}")
            .ToList();

        duplicateNames.ShouldBeEmpty();
    }

    [Fact]
    public void LinkType_ids_are_unique()
    {
        var linkTypes = DiscoverLinkTypes();
        linkTypes.ShouldNotBeEmpty();

        var duplicateIds = linkTypes
            .GroupBy(lt => lt.Id)
            .Where(g => g.Count() > 1)
            .Select(g => $"Duplicate LinkType Id {g.Key}: {string.Join(", ", g.Select(lt => lt.Name))}")
            .ToList();

        duplicateIds.ShouldBeEmpty();
    }

    private static List<LinkType> DiscoverLinkTypes() =>
        typeof(IdentityServerConstants).Assembly.DefinedTypes
            .SelectMany(type => type.DeclaredFields
                .Where(f => f.IsStatic && f.FieldType == typeof(LinkDefinition))
                .Select(f => ((LinkDefinition)f.GetValue(null)!).Link))
            .ToList();

    private static List<EntityType> DiscoverEntityTypes()
    {
        var assembly = typeof(IdentityServerConstants).Assembly;

        return assembly.DefinedTypes
            .Where(type => type.Name.EndsWith("Dso", StringComparison.Ordinal))
            .SelectMany(type => type.DeclaredFields
                .Where(f => f.IsStatic && f.FieldType == typeof(EntityType))
                .Select(f => (EntityType)f.GetValue(null)!))
            .ToList();
    }
}
