// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.EntityFramework.Tests
{
    public class UserSessionStoreTests
    {
        private readonly IUserSessionStore _subject;
        private readonly SessionDbContext _database;

        public UserSessionStoreTests()
        {
            var services = new ServiceCollection();
            services.AddBff()
                .AddEntityFrameworkServerSideSessions(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            var provider = services.BuildServiceProvider();

            _subject = provider.GetRequiredService<IUserSessionStore>();
            _database = provider.GetRequiredService<SessionDbContext>();
        }

        [Fact]
        public async Task CreateUserSessionAsync_should_succeed()
        {
            _database.UserSessions.Count().ShouldBe(0);

            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = "key123",
                SessionId = "sid",
                SubjectId = "sub",
                Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                Renewed = new DateTime(2021, 4, 2, 10, 13, 34, DateTimeKind.Utc),
                Expires = new DateTime(2022, 5, 3, 11, 14, 35, DateTimeKind.Utc),
                Ticket = "ticket"
            });

            _database.UserSessions.Count().ShouldBe(1);
        }


        [Fact]
        public async Task GetUserSessionAsync_for_valid_key_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = "key123",
                SessionId = "sid",
                SubjectId = "sub",
                Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                Renewed = new DateTime(2021, 4, 2, 10, 13, 34, DateTimeKind.Utc),
                Expires = new DateTime(2022, 5, 3, 11, 14, 35, DateTimeKind.Utc),
                Ticket = "ticket"
            });

            var item = await _subject.GetUserSessionAsync("key123");

            item.ShouldNotBeNull();
            item.Key.ShouldBe("key123");
            item.SubjectId.ShouldBe("sub");
            item.SessionId.ShouldBe("sid");
            item.Ticket.ShouldBe("ticket");
            item.Created.ShouldBe(new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc));
            item.Renewed.ShouldBe(new DateTime(2021, 4, 2, 10, 13, 34, DateTimeKind.Utc));
            item.Expires.ShouldBe(new DateTime(2022, 5, 3, 11, 14, 35, DateTimeKind.Utc));
        }
        [Fact]
        public async Task GetUserSessionAsync_for_invalid_key_should_return_null()
        {
            var item = await _subject.GetUserSessionAsync("invalid");
            item.ShouldBeNull();
        }


        [Fact]
        public async Task UpdateUserSessionAsync_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = "key123",
                SessionId = "sid",
                SubjectId = "sub",
                Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                Renewed = new DateTime(2021, 4, 2, 10, 13, 34, DateTimeKind.Utc),
                Expires = new DateTime(2022, 5, 3, 11, 14, 35, DateTimeKind.Utc),
                Ticket = "ticket"
            });

            {
                await _subject.UpdateUserSessionAsync("key123", new UserSessionUpdate
                {
                    Ticket = "ticket2",
                    SessionId = "sid",
                    SubjectId = "sub",
                    Created = new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                    Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
                    Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
                });

                var item = await _subject.GetUserSessionAsync("key123");
                item.ShouldNotBeNull();
                item.Key.ShouldBe("key123");
                item.SubjectId.ShouldBe("sub");
                item.SessionId.ShouldBe("sid");
                item.Ticket.ShouldBe("ticket2");
                item.Created.ShouldBe(new DateTime(2020, 3, 1, 9, 12, 33, DateTimeKind.Utc));
                item.Renewed.ShouldBe(new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc));
                item.Expires.ShouldBe(new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc));
            }
            {
                await _subject.UpdateUserSessionAsync("key123", new UserSessionUpdate
                {
                    Ticket = "ticket3",
                    SessionId = "sid2",
                    SubjectId = "sub2",
                    Created = new DateTime(2022, 3, 1, 9, 12, 33, DateTimeKind.Utc),
                    Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
                    Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
                });

                var item = await _subject.GetUserSessionAsync("key123");
                item.ShouldNotBeNull();
                item.Key.ShouldBe("key123");
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
            await _subject.UpdateUserSessionAsync("key123", new UserSessionUpdate
            {
                Ticket = "ticket2",
                Renewed = new DateTime(2024, 1, 3, 5, 7, 9, DateTimeKind.Utc),
                Expires = new DateTime(2025, 2, 4, 6, 8, 10, DateTimeKind.Utc)
            });

            var item = await _subject.GetUserSessionAsync("key123");
            item.ShouldBeNull();
        }


        [Fact]
        public async Task DeleteUserSessionAsync_for_valid_key_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = "key123",
                SubjectId = "sub",
                SessionId = "session",
                Ticket = "ticket",
            });
            _database.UserSessions.Count().ShouldBe(1);

            await _subject.DeleteUserSessionAsync("key123");

            _database.UserSessions.Count().ShouldBe(0);
        }
        [Fact]
        public async Task DeleteUserSessionAsync_for_invalid_key_should_succeed()
        {
            await _subject.DeleteUserSessionAsync("invalid");
        }


        [Fact]
        public async Task GetUserSessionsAsync_for_valid_sub_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub2" });
            items.Count().ShouldBe(3);
            items.Select(x => x.SubjectId).Distinct().ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
            items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_1", "sid2_2", "sid2_3", });
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_invalid_sub_should_return_empty()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid" });
            items.Count().ShouldBe(0);
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_valid_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SessionId = "sid2_2" });
            items.Count().ShouldBe(1);
            items.Select(x => x.SubjectId).ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
            items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_2" });
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_invalid_sid_should_return_empty()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SessionId = "invalid" });
            items.Count().ShouldBe(0);
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_valid_sub_and_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub2", SessionId = "sid2_2" });
            items.Count().ShouldBe(1);
            items.Select(x => x.SubjectId).ToArray().ShouldBeEquivalentTo(new[] { "sub2" });
            items.Select(x => x.SessionId).ToArray().ShouldBeEquivalentTo(new[] { "sid2_2" });
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_invalid_sub_and_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            {
                var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid", SessionId = "invalid" });
                items.Count().ShouldBe(0);
            }
            {
                var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub1", SessionId = "invalid" });
                items.Count().ShouldBe(0);
            }
            {
                var items = await _subject.GetUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid", SessionId = "sid1_1" });
                items.Count().ShouldBe(0);
            }
        }
        [Fact]
        public async Task GetUserSessionsAsync_for_missing_sub_and_sid_should_throw()
        {
            Func<Task> f = () => _subject.GetUserSessionsAsync(new UserSessionsFilter());
            await f.ShouldThrowAsync<Exception>();
        }


        [Fact]
        public async Task DeleteUserSessionsAsync_for_valid_sub_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub2" });
            _database.UserSessions.Count().ShouldBe(3);
            _database.UserSessions.Count(x => x.SubjectId == "sub2").ShouldBe(0);
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_invalid_sub_should_do_nothing()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid" });
            _database.UserSessions.Count().ShouldBe(6);
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_valid_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SessionId = "sid2_2" });
            _database.UserSessions.Count().ShouldBe(5);
            _database.UserSessions.Count(x => x.SessionId == "sid2_2").ShouldBe(0);
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_invalid_sid_should_do_nothing()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SessionId = "invalid" });
            _database.UserSessions.Count().ShouldBe(6);
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_valid_sub_and_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub2", SessionId = "sid2_2" });
            _database.UserSessions.Count().ShouldBe(5);
            _database.UserSessions.Count(x => x.SubjectId == "sub2" && x.SessionId == "sid2_2").ShouldBe(0);
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_invalid_sub_and_sid_should_succeed()
        {
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub1",
                SessionId = "sid1_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_1",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_2",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub2",
                SessionId = "sid2_3",
            });
            await _subject.CreateUserSessionAsync(new UserSession
            {
                Key = Guid.NewGuid().ToString(),
                Ticket = "ticket",
                SubjectId = "sub3",
                SessionId = "sid3_1",
            });

            {
                await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid", SessionId = "invalid" });
                _database.UserSessions.Count().ShouldBe(6);
            }
            {
                await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "sub1", SessionId = "invalid" });
                _database.UserSessions.Count().ShouldBe(6);
            }
            {
                await _subject.DeleteUserSessionsAsync(new UserSessionsFilter { SubjectId = "invalid", SessionId = "sid1_1" });
                _database.UserSessions.Count().ShouldBe(6);
            }
        }
        [Fact]
        public async Task DeleteUserSessionsAsync_for_missing_sub_and_sid_should_throw()
        {
            Func<Task> f = () => _subject.DeleteUserSessionsAsync(new UserSessionsFilter());
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
            var key = Guid.NewGuid().ToString();
            ctx0.UserSessions.Add(new UserSessionEntity
            {
                Key = key,
                Ticket = "ticket",
                ApplicationName = "app",
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
    }
}
