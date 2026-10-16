// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Duende.Spaces.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces;

public sealed class RunCodeInASpaceHowToTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceContextAccessor _accessor = null!;
    private ISpaceAdmin _admin = null!;
    private IDataProtectionProvider _dataProtectionProvider = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpacesInternal();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = _services.GetRequiredService<IStorageInstanceSchema>();
        await storageInstanceSchema.MigrateAsync(_ct);

        _accessor = _services.GetRequiredService<ISpaceContextAccessor>();
        _admin = _services.GetRequiredService<ISpaceAdmin>();
        _dataProtectionProvider = _services.GetRequiredService<IDataProtectionProvider>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public void get_space_id_throws_when_nothing_is_set() =>
        Should.Throw<InvalidOperationException>(() => _accessor.GetSpaceId());

    [Fact]
    public void read_current_describes_unset_and_set_space()
    {
        var accessor = _accessor;

        // begin-snippet: run-code-in-a-space-read-current
        var description = accessor.IsSpaceIdConfigured()
            ? $"running in space {accessor.GetSpaceIdOrDefault()}"
            : "no space context has been set";
        // end-snippet
        description.ShouldBe("no space context has been set");

        var spaceId = SpaceId.New();
        using (accessor.SetSpace(spaceId))
        {
            (accessor.IsSpaceIdConfigured()
                ? $"running in space {accessor.GetSpaceIdOrDefault()}"
                : "no space context has been set").ShouldBe($"running in space {spaceId}");
        }
    }

    [Fact]
    public async Task space_flows_across_await()
    {
        var accessor = _accessor;
        var dataProtectionProvider = _dataProtectionProvider;
        var spaceId = SpaceId.New();

        // begin-snippet: run-code-in-a-space-set-space
        // Entering a space is a disposable scope: dispose it to restore whatever space (if
        // any) was active before, even if an exception is thrown while inside.
        using (accessor.SetSpace(spaceId))
        {
            var protector = dataProtectionProvider.CreateProtector("background-job");
            await Task.Yield();
            protector.Protect("payload");
        }
        // end-snippet

        accessor.IsSpaceIdConfigured().ShouldBeFalse();
    }

    [Fact]
    public void protector_throws_outside_a_space_and_works_inside_one()
    {
        var bytes = "payload"u8.ToArray();
        var protector = _dataProtectionProvider.CreateProtector("background-job");

        Should.Throw<InvalidOperationException>(() => protector.Protect(bytes));

        using (_accessor.SetSpace(SpaceId.New()))
        {
            var protectedBytes = protector.Protect(bytes);
            protector.Unprotect(protectedBytes).ShouldBe(bytes);
        }
    }

    [Fact]
    public void set_space_restores_the_previous_space_on_dispose()
    {
        var outer = SpaceId.New();
        var inner = SpaceId.New();

        using (_accessor.SetSpace(outer))
        {
            using (_accessor.SetSpace(inner))
            {
                _accessor.GetSpaceId().ShouldBe(inner);
            }

            _accessor.GetSpaceId().ShouldBe(outer);
        }

        _accessor.IsSpaceIdConfigured().ShouldBeFalse();
    }

    [Fact]
    public void disposing_scopes_out_of_order_throws()
    {
        var outer = _accessor.SetSpace(SpaceId.New());
        var inner = _accessor.SetSpace(SpaceId.New());

        Should.Throw<InvalidOperationException>(outer.Dispose);

        inner.Dispose();
        outer.Dispose();
    }

    [Fact]
    public async Task background_job_sets_a_space_for_every_space_it_touches()
    {
        for (var i = 1; i <= 3; i++)
        {
            await _admin.CreateAsync(
                new CreateSpaceConfiguration
                {
                    Name = $"Space {i}",
                    MatchPatterns = [new SpaceMatchPattern { Origin = $"https://space{i}.example.com" }]
                },
                _ct);
        }

        var disabled = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Disabled space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://disabled.example.com" }]
            },
            _ct);
        var disabledRead = await _admin.GetAsync(disabled.Id!, _ct);
        disabledRead.Item!.Enabled = false;
        await _admin.UpdateAsync(disabled.Id!, disabledRead.Item, disabledRead.Version!, _ct);

        var deleted = await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Deleted space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://deleted.example.com" }]
            },
            _ct);
        await _admin.DeleteAsync(deleted.Id!, _ct);

        var accessor = _accessor;
        var admin = _admin;
        var dataProtectionProvider = _dataProtectionProvider;
        var ct = _ct;

        // begin-snippet: run-code-in-a-space-background-job
        var touched = 0;
        DataRange? range = null;

        do
        {
            var page = await admin.QueryAsync(
                new QueryRequest<SpaceFilter, SpaceSortField>
                {
                    Filter = new SpaceFilter { Enabled = true, IsDeleted = false },
                    Range = range
                },
                ct);

            foreach (var space in page.Items)
            {
                // A background job has no ambient request, so it must set the space itself
                // before doing any space-scoped work, such as protecting data.
                using (accessor.SetSpace(space.Id))
                {
                    _ = dataProtectionProvider.CreateProtector("secrets").Protect("rotated");
                    touched++;
                }
            }

            range = page.HasMoreData && page.NextToken is { } nextToken
                ? DataRange.FromContinuationToken(nextToken)
                : null;
        }
        while (range is not null);
        // end-snippet

        touched.ShouldBe(3);
        accessor.IsSpaceIdConfigured().ShouldBeFalse();
    }

    [Fact]
    public void nested_override_runs_in_the_overriding_space_then_restores()
    {
        var accessor = _accessor;
        var requestSpace = SpaceId.New();

        using (accessor.SetSpace(requestSpace))
        {
            // begin-snippet: run-code-in-a-space-nested-override
            // A request handler's own space stays active for everything except the inner block:
            // nesting SetSpace temporarily overrides it, and disposing restores it.
            SpaceId overriddenSpace;
            using (accessor.SetSpace(SpaceId.Management))
            {
                overriddenSpace = accessor.GetSpaceId();
            }
            // end-snippet

            overriddenSpace.ShouldBe(SpaceId.Management);
            accessor.GetSpaceId().ShouldBe(requestSpace);
        }
    }

    [Fact]
    public void nested_scopes_produce_a_tagged_space_override_activity()
    {
        var stopped = new ConcurrentBag<Activity>();

        // begin-snippet: run-code-in-a-space-trace-listener
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SpacesTracing.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        // end-snippet

        var outer = SpaceId.New();
        var inner = SpaceId.New();

        using (_accessor.SetSpace(outer))
        {
            using (_accessor.SetSpace(inner))
            {
            }
        }

        // ActivityListener is process-global, so other tests running in parallel also feed
        // this listener. Filter to the activities tagged with this test's own space.
        var overrideActivities = stopped
            .Where(a => a.OperationName == "space.override" && a.GetTagItem("duende.space.id") as string == inner.Value.ToString())
            .ToList();
        overrideActivities.ShouldHaveSingleItem();
    }
}
