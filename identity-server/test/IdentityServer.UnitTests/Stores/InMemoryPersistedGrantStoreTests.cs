// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace UnitTests.Stores;

public class InMemoryPersistedGrantStoreTests
{
    private InMemoryPersistedGrantStore _subject;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public InMemoryPersistedGrantStoreTests() => _subject = new InMemoryPersistedGrantStore();

    [Fact]
    public async Task Store_should_persist_value()
    {
        {
            var item = await _subject.GetAsync("key1", _ct);
            item.ShouldBeNull();
        }

        await _subject.StoreAsync(new PersistedGrant() { Key = "key1" }, _ct);

        {
            var item = await _subject.GetAsync("key1", _ct);
            item.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GetAll_should_filter()
    {
        await _subject.StoreAsync(new PersistedGrant() { Key = "key1", SubjectId = "sub1", ClientId = "client1", SessionId = "session1" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key2", SubjectId = "sub1", ClientId = "client2", SessionId = "session1" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key3", SubjectId = "sub1", ClientId = "client1", SessionId = "session2" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key4", SubjectId = "sub1", ClientId = "client3", SessionId = "session2" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key5", SubjectId = "sub1", ClientId = "client4", SessionId = "session3" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key6", SubjectId = "sub1", ClientId = "client4", SessionId = "session4" }, _ct);

        await _subject.StoreAsync(new PersistedGrant() { Key = "key7", SubjectId = "sub2", ClientId = "client4", SessionId = "session4" }, _ct);



        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key1", "key2", "key3", "key4", "key5", "key6"], true);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key7"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub3"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client1"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key1", "key3"], true);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client2"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key2"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client3"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key4"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client4"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key5", "key6"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client5"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2",
            ClientId = "client1"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2",
            ClientId = "client4"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key7"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub3",
            ClientId = "client1"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client1",
            SessionId = "session1"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key1"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client1",
            SessionId = "session2"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key3"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client1",
            SessionId = "session3"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client2",
            SessionId = "session1"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key2"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client2",
            SessionId = "session2"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub1",
            ClientId = "client4",
            SessionId = "session4"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key6"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2",
            ClientId = "client4",
            SessionId = "session4"
        }, _ct))
            .Select(x => x.Key).ShouldBe(["key7"]);

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2",
            ClientId = "client4",
            SessionId = "session1"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();

        (await _subject.GetAllAsync(new PersistedGrantFilter
        {
            SubjectId = "sub2",
            ClientId = "client4",
            SessionId = "session5"
        }, _ct))
            .Select(x => x.Key).ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAll_should_filter()
    {
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub2"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub3"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client2"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client3"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client4"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client5"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub2",
                ClientId = "client1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client4"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub3",
                ClientId = "client1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client1",
                SessionId = "session1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client1",
                SessionId = "session2"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client1",
                SessionId = "session3"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client2",
                SessionId = "session1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client2",
                SessionId = "session2"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub1",
                ClientId = "client4",
                SessionId = "session4"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub2",
                ClientId = "client4",
                SessionId = "session4"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub2",
                ClientId = "client4",
                SessionId = "session1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub2",
                ClientId = "client4",
                SessionId = "session5"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
        {
            await Populate();
            await _subject.RemoveAllAsync(new PersistedGrantFilter
            {
                SubjectId = "sub3",
                ClientId = "client1",
                SessionId = "session1"
            }, _ct);
            (await _subject.GetAsync("key1", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key2", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key3", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key4", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key5", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key6", _ct)).ShouldNotBeNull();
            (await _subject.GetAsync("key7", _ct)).ShouldNotBeNull();
        }
    }

    private async Task Populate()
    {
        _subject = new InMemoryPersistedGrantStore();
        await _subject.StoreAsync(new PersistedGrant() { Key = "key1", SubjectId = "sub1", ClientId = "client1", SessionId = "session1" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key2", SubjectId = "sub1", ClientId = "client2", SessionId = "session1" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key3", SubjectId = "sub1", ClientId = "client1", SessionId = "session2" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key4", SubjectId = "sub1", ClientId = "client3", SessionId = "session2" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key5", SubjectId = "sub1", ClientId = "client4", SessionId = "session3" }, _ct);
        await _subject.StoreAsync(new PersistedGrant() { Key = "key6", SubjectId = "sub1", ClientId = "client4", SessionId = "session4" }, _ct);

        await _subject.StoreAsync(new PersistedGrant() { Key = "key7", SubjectId = "sub2", ClientId = "client4", SessionId = "session4" }, _ct);
    }
}
