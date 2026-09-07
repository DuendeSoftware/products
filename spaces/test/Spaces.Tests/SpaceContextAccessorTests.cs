// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public class SpaceContextAccessorTests
{
    private readonly ISpaceContextAccessor _sut;

    public SpaceContextAccessorTests()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        var sp = sc.BuildServiceProvider();
        _sut = sp.GetRequiredService<ISpaceContextAccessor>();
    }

    [Fact]
    public void get_space_id_throws_when_not_set()
    {
        var act = () => _sut.GetSpaceId();
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void is_space_id_configured_returns_false_when_not_set() =>
        _sut.IsSpaceIdConfigured().ShouldBeFalse();

    [Fact]
    public void set_space_then_get_returns_same_value()
    {
        var spaceId = SpaceId.New();
        using var scope = _sut.SetSpace(spaceId);

        _sut.GetSpaceId().ShouldBe(spaceId);
        _sut.IsSpaceIdConfigured().ShouldBeTrue();
    }

    [Fact]
    public void set_space_changes_current_space()
    {
        var original = SpaceId.New();
        var next = SpaceId.New();
        using (_sut.SetSpace(original))
        {
            _sut.GetSpaceId().ShouldBe(original);
        }

        using (_sut.SetSpace(next))
        {
            _sut.GetSpaceId().ShouldBe(next);
        }
    }

    [Fact]
    public void set_space_restores_previous_on_dispose()
    {
        var original = SpaceId.New();
        var temp = SpaceId.New();
        using var outer = _sut.SetSpace(original);

        var inner = _sut.SetSpace(temp);
        _sut.GetSpaceId().ShouldBe(temp);

        inner.Dispose();
        _sut.GetSpaceId().ShouldBe(original);
    }

    [Fact]
    public void set_space_restores_null_when_no_previous()
    {
        var temp = SpaceId.New();
        var scope = _sut.SetSpace(temp);
        _sut.GetSpaceId().ShouldBe(temp);

        scope.Dispose();
        _sut.IsSpaceIdConfigured().ShouldBeFalse();
    }

    [Fact]
    public void nested_set_space_restores_correctly()
    {
        var level0 = SpaceId.New();
        var level1 = SpaceId.New();
        var level2 = SpaceId.New();

        using (_sut.SetSpace(level0))
        {
            _sut.GetSpaceId().ShouldBe(level0);

            using (_sut.SetSpace(level1))
            {
                _sut.GetSpaceId().ShouldBe(level1);

                using (_sut.SetSpace(level2))
                {
                    _sut.GetSpaceId().ShouldBe(level2);
                }

                _sut.GetSpaceId().ShouldBe(level1);
            }

            _sut.GetSpaceId().ShouldBe(level0);
        }

        _sut.IsSpaceIdConfigured().ShouldBeFalse();
    }

    [Fact]
    public void double_dispose_is_safe()
    {
        var original = SpaceId.New();
        using var outer = _sut.SetSpace(original);

        var inner = _sut.SetSpace(SpaceId.New());
        inner.Dispose();
        _sut.GetSpaceId().ShouldBe(original);

        // Second dispose should be a no-op
        inner.Dispose();
        _sut.GetSpaceId().ShouldBe(original);
    }

    [Fact]
    public void double_dispose_does_not_clobber_subsequent_scope()
    {
        var original = SpaceId.New();
        var second = SpaceId.New();
        using var outer = _sut.SetSpace(original);

        var scope1 = _sut.SetSpace(SpaceId.New());
        scope1.Dispose();

        // Now set a new scope
        using (_sut.SetSpace(second))
        {
            // Double-disposing scope1 must not restore to "original"
            scope1.Dispose();
            _sut.GetSpaceId().ShouldBe(second);
        }

        _sut.GetSpaceId().ShouldBe(original);
    }

    [Fact]
    public void disposing_scopes_out_of_order_throws_and_preserves_current_space()
    {
        var outerSpace = SpaceId.New();
        var innerSpace = SpaceId.New();
        var outer = _sut.SetSpace(outerSpace);
        var inner = _sut.SetSpace(innerSpace);

        var act = outer.Dispose;

        act.ShouldThrow<InvalidOperationException>()
            .Message.ShouldBe("Space scopes must be disposed in reverse order.");
        _sut.GetSpaceId().ShouldBe(innerSpace);

        inner.Dispose();
        _sut.GetSpaceId().ShouldBe(outerSpace);

        outer.Dispose();
        _sut.IsSpaceIdConfigured().ShouldBeFalse();
    }
}
