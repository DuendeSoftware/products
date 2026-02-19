// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.Tests.SessionManagement;
using Duende.Bff.Tests.TestInfra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserSessionDb;

namespace Duende.Bff.EntityFramework.Tests;

public class UserSessionStoreTests : IAsyncLifetime
{
    private readonly IUserSessionStore _subject;
    private readonly SessionDbContext _database;
    private FakeHttpContextAccessor _fakeHttpContextAccessor;
    private ServiceProvider _provider;

    public TestData The = new TestData();
    public TestDataBuilder Some => new TestDataBuilder(The);
    private readonly string _dbFilePath;

    private UserSessionKey invalidUserSessionKey => new UserSessionKey(The.PartitionKey, UserKey.Parse("wrong"));

    public UserSessionStoreTests()
    {
        var output = TestContext.Current.TestOutputHelper!;
        var services = new ServiceCollection();
        _fakeHttpContextAccessor = new FakeHttpContextAccessor();
        _dbFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.sqlite");
        var connectionString = $"Data Source={_dbFilePath}";

        services
            .AddSingleton<CurrentFrontendAccessor>()
            .AddSingleton<IHttpContextAccessor>(_fakeHttpContextAccessor)
            .AddLogging(l => l.AddProvider(new TestLoggerProvider(output.WriteLine, "db")))
            .AddBff()
            .AddEntityFrameworkServerSideSessions(options => options.UseSqlite(connectionString, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName)));

        services.AddDataProtection(s => s.ApplicationDiscriminator = "bob");

        _provider = services.BuildServiceProvider();

        _subject = _provider.GetRequiredService<IUserSessionStore>();
        _database = _provider.GetRequiredService<SessionDbContext>();

    }

    [Fact]
    public async Task CreateUserSessionAsync_should_succeed()
    {
        _database.UserSessions.Count().ShouldBe(0);

        await _subject.CreateUserSessionAsync(Some.UserSession());

        _database.UserSessions.Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateUserSessions_sessions_are_unique_per_frontend()
    {
        await _subject.CreateUserSessionAsync(Some.UserSession());
        await _subject.CreateUserSessionAsync(Some.UserSession());

        _database.UserSessions.Count().ShouldBe(1);
    }

    [Fact]
    public async Task CreateUserSessions_are_partitioned_per_frontend()
    {
        await _subject.CreateUserSessionAsync(Some.UserSession());

        using (UseFrontend(Some.BffFrontend()))
        {
            await _subject.CreateUserSessionAsync(Some.UserSession());
        }

        _database.UserSessions.Count().ShouldBe(2);
    }

    [Fact]
    public async Task GetUserSessionAsync_should_work_for_multi_frontends()
    {
        // Create a session (without frontend)
        await _subject.CreateUserSessionAsync(Some.UserSession());

        var item = await _subject.GetUserSessionAsync(The.UserSessionKey);

        // Now create a session (same sid, different ticket) in a different frontend
        using (UseFrontend(Some.BffFrontend()))
        {
            await _subject.CreateUserSessionAsync(Some.UserSession().With(x => x.Ticket = "different"));
            var item2 = await _subject.GetUserSessionAsync(The.UserSessionKey);

            // The session in the different frontend should not be the same as the one we created first
            item2.ShouldBeEquivalentTo(Some.UserSession().With(x => x.Ticket = "different"));
        }

        item.ShouldBeEquivalentTo(Some.UserSession());

    }

    [Fact]
    public async Task GetUserSessionAsync_for_valid_key_should_succeed()
    {
        // Create a session (without frontend)
        await _subject.CreateUserSessionAsync(Some.UserSession());

        var item = await _subject.GetUserSessionAsync(The.UserSessionKey);
        item.ShouldBeEquivalentTo(Some.UserSession());
    }



    [Fact]
    public async Task GetUserSessionAsync_for_invalid_key_should_return_null()
    {
        var item = await _subject.GetUserSessionAsync(new UserSessionKey(The.PartitionKey, UserKey.Parse("wrong")));
        item.ShouldBeNull();
    }


    [Fact]
    public async Task UpdateUserSessionAsync_should_succeed()
    {
        await _subject.CreateUserSessionAsync(Some.UserSession());

        {
            await _subject.UpdateUserSessionAsync(The.UserSessionKey, new UserSessionUpdate
            {
                Ticket = "ticket2",
                SessionId = "sid",
                SubjectId = "sub",
                Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
                Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
            });

            var item = await _subject.GetUserSessionAsync(The.UserSessionKey);
            item.ShouldNotBeNull();
            item.Key.ShouldBe(The.UserKey);
            item.SubjectId.ShouldBe("sub");
            item.SessionId.ShouldBe("sid");
            item.Ticket.ShouldBe("ticket2");
            item.Created.ShouldBe(new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc));
            item.Renewed.ShouldBe(new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc));
            item.Expires.ShouldBe(new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc));
        }
        {
            await _subject.UpdateUserSessionAsync(The.UserSessionKey, new UserSessionUpdate
            {
                Ticket = "ticket3",
                SessionId = "sid2",
                SubjectId = "sub2",
                Created = new DateTime(2022, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
                Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
            });

            var item = await _subject.GetUserSessionAsync(The.UserSessionKey);
            item.ShouldNotBeNull();
            item.Key.ShouldBe(The.UserKey);
            item.SubjectId.ShouldBe("sub2");
            item.SessionId.ShouldBe("sid2");
            item.Ticket.ShouldBe("ticket3");
            item.Created.ShouldBe(new DateTime(2022, 3, 1, 9, 12, 33, DateTimeKind.Utc));
            item.Renewed.ShouldBe(new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc));
            item.Expires.ShouldBe(new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc));
        }
    }

    [Fact]
    public async Task UpdateUserSessionAsync_for_invalid_key_should_succeed()
    {
        await _subject.UpdateUserSessionAsync(The.UserSessionKey, new UserSessionUpdate
        {
            Ticket = "ticket2",
            Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
            Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
        });

        var item = await _subject.GetUserSessionAsync(The.UserSessionKey);
        item.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteUserSessionAsync_for_valid_key_should_succeed()
    {
        await _subject.CreateUserSessionAsync(Some.UserSession());
        _database.UserSessions.Count().ShouldBe(1);

        await _subject.DeleteUserSessionAsync(The.UserSessionKey);

        _database.UserSessions.Count().ShouldBe(0);
    }

    [Fact]
    public async Task DeleteUserSessionAsync_respects_multi_frontends()
    {
        await _subject.CreateUserSessionAsync(Some.UserSession());
        using (UseFrontend(Some.BffFrontend()))
        {
            await _subject.CreateUserSessionAsync(Some.UserSession());
        }
        _database.UserSessions.Count().ShouldBe(2);


        await _subject.DeleteUserSessionAsync(The.UserSessionKey);

        _database.UserSessions.Count().ShouldBe(1);

        using (UseFrontend(Some.BffFrontend()))
        {
            await _subject.DeleteUserSessionAsync(The.UserSessionKey);
        }
        _database.UserSessions.Count().ShouldBe(0);

    }

    [Fact]
    public async Task DeleteUserSessionAsync_for_invalid_key_should_succeed() =>
        await _subject.DeleteUserSessionAsync(invalidUserSessionKey);

    [Fact]
    public async Task GetUserSessionsAsync_for_valid_sub_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SubjectId = "sub2"
        });
        items.Count().ShouldBe(3);
        items.Select(x => x.SubjectId).Distinct().ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
        items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_1", "sid2_2", "sid2_3", });
    }

    private UserSession BuildUserSessionWithRandomId() => new UserSession()
    {
        Key = UserKey.Parse(Guid.NewGuid().ToString()),
        PartitionKey = The.PartitionKey,
        Ticket = "ticket"
    };

    [Fact]
    public async Task GetUserSessionsAsync_for_invalid_sub_should_return_empty()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SubjectId = "invalid"
        });
        items.Count().ShouldBe(0);
    }

    [Fact]
    public async Task GetUserSessionsAsync_for_valid_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SessionId = "sid2_2"
        });
        items.Count().ShouldBe(1);
        items.Select(x => x.SubjectId).ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
        items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_2" });
    }

    [Fact]
    public async Task GetUserSessionsAsync_for_invalid_sid_should_return_empty()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SessionId = "invalid"
        });
        items.Count().ShouldBe(0);
    }

    [Fact]
    public async Task GetUserSessionsAsync_for_valid_sub_and_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SubjectId = "sub2",
            SessionId = "sid2_2"
        });
        items.Count().ShouldBe(1);
        items.Select(x => x.SubjectId).ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
        items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_2" });
    }

    [Fact]
    public async Task GetUserSessionsAsync_for_invalid_sub_and_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        {
            var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "invalid",
                SessionId = "invalid"
            });
            items.Count().ShouldBe(0);
        }
        {
            var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "sub1",
                SessionId = "invalid"
            });
            items.Count().ShouldBe(0);
        }
        {
            var items = await _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "invalid",
                SessionId = "sid1_1"
            });
            items.Count().ShouldBe(0);
        }
    }

    [Fact]
    public async Task GetUserSessionsAsync_for_missing_sub_and_sid_should_throw()
    {
        Func<Task> f = () => _subject.GetUserSessionsAsync(The.PartitionKey, new UserSessionsFilter()
        {
        });
        await f.ShouldThrowAsync<Exception>();
    }


    [Fact]
    public async Task DeleteUserSessionsAsync_for_valid_sub_should_succeed()
    {

        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        })); ;

        await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SubjectId = "sub2"
        });
        _database.UserSessions.Count().ShouldBe(3);
        _database.UserSessions.Count(x => x.SubjectId == "sub2").ShouldBe(0);
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_invalid_sub_should_do_nothing()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
        {
            SubjectId = "invalid"
        });
        _database.UserSessions.Count().ShouldBe(6);
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_valid_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter { SessionId = "sid2_2" });
        _database.UserSessions.Count().ShouldBe(5);
        _database.UserSessions.Count(x => x.SessionId == "sid2_2").ShouldBe(0);
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_invalid_sid_should_do_nothing()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));
        await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter { SessionId = "invalid" });
        _database.UserSessions.Count().ShouldBe(6);
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_valid_sub_and_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter { SubjectId = "sub2", SessionId = "sid2_2" });
        _database.UserSessions.Count().ShouldBe(5);
        _database.UserSessions.Count(x => x.SubjectId == "sub2" && x.SessionId == "sid2_2").ShouldBe(0);
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_invalid_sub_and_sid_should_succeed()
    {
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub1";
            x.SessionId = "sid1_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_1";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_2";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub2";
            x.SessionId = "sid2_3";
        }));
        await _subject.CreateUserSessionAsync(BuildUserSessionWithRandomId().With(x =>
        {
            x.SubjectId = "sub3";
            x.SessionId = "sid3_1";
        }));

        {
            await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "invalid",
                SessionId = "invalid"
            });
            _database.UserSessions.Count().ShouldBe(6);
        }
        {
            await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "sub1",
                SessionId = "invalid"
            });
            _database.UserSessions.Count().ShouldBe(6);
        }
        {
            await _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter
            {
                SubjectId = "invalid",
                SessionId = "sid1_1"
            });
            _database.UserSessions.Count().ShouldBe(6);
        }
    }

    [Fact]
    public async Task DeleteUserSessionsAsync_for_missing_sub_and_sid_should_throw()
    {
        Func<Task> f = () => _subject.DeleteUserSessionsAsync(The.PartitionKey, new UserSessionsFilter()
        {
        });
        await f.ShouldThrowAsync<Exception>();
    }

    [Fact]
    public async Task concurrent_deletes_with_exception_handler_and_detatching_should_succeed()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddBff()
            .AddEntityFrameworkServerSideSessions(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        using var scope0 = provider.CreateScope();
        var ctx0 = scope0.ServiceProvider.GetRequiredService<SessionDbContext>();
        var key = UserKey.Parse(Guid.NewGuid().ToString());
        ctx0.UserSessions.Add(new UserSessionEntity
        {
            PartitionKey = The.PartitionKey,
            Key = key,
            Ticket = "ticket",
            SubjectId = "sub",
            SessionId = "sid",
        });
        await ctx0.SaveChangesAsync();

        using var scope1 = provider.CreateScope();
        var ctx1 = scope1.ServiceProvider.GetRequiredService<SessionDbContext>();
        var item1 = ctx1.UserSessions.Single(x => x.Key == key);
        ctx1.UserSessions.Remove(item1);

        using var scope2 = provider.CreateScope();
        var ctx2 = scope2.ServiceProvider.GetRequiredService<SessionDbContext>();
        var item2 = ctx2.UserSessions.Single(x => x.Key == key);
        ctx2.UserSessions.Remove(item2);

        await ctx1.SaveChangesAsync();

        Func<Task> f1 = async () => await ctx2.SaveChangesAsync();
        await f1.ShouldThrowAsync<DbUpdateConcurrencyException>();

        try
        {
            await ctx2.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                // mark detatched so another call to SaveChangesAsync won't throw again
                entry.State = EntityState.Detached;
            }
        }

        // calling again to not throw
        await ctx2.SaveChangesAsync();
    }

    public IDisposable UseFrontend(BffFrontend frontent)
    {
        var original = The.PartitionKey;
        The.PartitionKey = PartitionKey.Parse(frontent.Name.ToString());
        return new DelegateDisposable(() =>
        {
            The.PartitionKey = original;
        });
    }

    public async ValueTask InitializeAsync() =>
        // Ensure the database is created and migrations are applied
        await _database.Database.MigrateAsync();


    public async ValueTask DisposeAsync()
    {
        // Close all open SQLite connections used by EF
        var dbConn = _database.Database.GetDbConnection();
        if (dbConn is SqliteConnection sqliteConn)
        {
            // Ensure connection is closed
            sqliteConn.Close();
            // Also call CloseConnection if available (for pooled connections)
            SqliteConnection.ClearPool(sqliteConn);
        }

        await _database.DisposeAsync();
        await _provider.DisposeAsync();

        if (File.Exists(_dbFilePath))
        {
            File.Delete(_dbFilePath);
        }
    }
}
