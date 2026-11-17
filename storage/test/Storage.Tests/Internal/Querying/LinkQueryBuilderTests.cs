// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

public sealed class LinkQueryBuilderTests
{
    private static readonly EntityType User = new(7001, "User");
    private static readonly EntityType Group = new(7002, "Group");

    private static readonly LinkDefinition UserGroup = new()
    {
        Left = User,
        Right = Group,
        Link = new LinkType(7101, "UserGroup")
    };

    private static readonly EntityType Role = new(7003, "Role");

    private static readonly LinkDefinition GroupRole = new()
    {
        Left = Group,
        Right = Role,
        Link = new LinkType(7102, "GroupRole")
    };

    private static IReadOnlySet<UuidV7> Ids(int count) =>
        Enumerable.Range(0, count).Select(_ => UuidV7.New()).ToHashSet();

    [Fact]
    public void where_any_sets_the_anchor_ids_on_the_descriptor()
    {
        var ids = Ids(2);

        var query = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, ids).Build();

        query.WhereEntityIds.ShouldBe(ids);
        query.WhereEntityId.ShouldBeNull();
    }

    [Fact]
    public void where_any_accepts_the_maximum_number_of_ids() =>
        _ = LinkQuery.From(User).Join(UserGroup)
            .WhereAny(Group, Ids(LinkQueryBuilder.MaxWhereAnyEntityIds));

    [Fact]
    public void where_any_rejects_more_than_the_maximum_number_of_ids()
    {
        var builder = LinkQuery.From(User).Join(UserGroup)
            .WhereAny(Group, Ids(LinkQueryBuilder.MaxWhereAnyEntityIds + 1));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Fact]
    public void descriptor_rejects_more_than_the_maximum_number_of_ids_when_constructed_directly() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new LinkQueryDescriptor(User, [new LinkQueryJoin(UserGroup, LinkJoinDirection.LeftToRight)], Group, null)
            {
                WhereEntityIds = Ids(LinkQueryBuilder.MaxWhereAnyEntityIds + 1)
            });

    [Fact]
    public void descriptor_is_not_affected_by_later_changes_to_the_callers_id_set()
    {
        var ids = Ids(LinkQueryBuilder.MaxWhereAnyEntityIds).ToHashSet();
        var query = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, ids).Build();

        _ = ids.Add(UuidV7.New());

        query.WhereEntityIds!.Count.ShouldBe(LinkQueryBuilder.MaxWhereAnyEntityIds);
    }

    [Fact]
    public void descriptor_is_not_affected_by_later_joins_on_the_builder()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, Ids(1));
        var query = builder.Build();

        _ = builder.Join(GroupRole);

        query.Joins.Count.ShouldBe(1);
        query.Joins[0].Definition.ShouldBe(UserGroup);
    }

    [Fact]
    public void where_any_cannot_be_followed_by_a_join_that_moves_the_terminal_type()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, Ids(1)).Join(GroupRole);

        _ = Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void where_cannot_be_followed_by_a_join_that_moves_the_terminal_type()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).Where(Group, UuidV7.New()).Join(GroupRole);

        _ = Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void where_any_rejects_a_type_that_is_not_the_terminal_type()
    {
        var builder = LinkQuery.From(User).Join(UserGroup);

        _ = Should.Throw<InvalidOperationException>(() => builder.WhereAny(User, Ids(1)));
    }

    [Fact]
    public void where_any_cannot_follow_where()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).Where(Group, UuidV7.New());

        _ = Should.Throw<InvalidOperationException>(() => builder.WhereAny(Group, Ids(1)));
    }

    [Fact]
    public void where_cannot_follow_where_any()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, Ids(1));

        _ = Should.Throw<InvalidOperationException>(() => builder.Where(Group, UuidV7.New()));
    }

    [Fact]
    public void where_any_cannot_be_set_twice()
    {
        var builder = LinkQuery.From(User).Join(UserGroup).WhereAny(Group, Ids(1));

        _ = Should.Throw<InvalidOperationException>(() => builder.WhereAny(Group, Ids(1)));
    }
}
