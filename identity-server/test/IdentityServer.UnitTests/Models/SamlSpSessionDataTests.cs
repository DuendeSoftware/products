// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml.Models;

namespace UnitTests.Models;

public class SamlSpSessionDataTests
{
    private const string Category = "SamlSpSessionData";

    [Fact]
    [Trait("Category", Category)]
    public void equals_should_return_true_for_same_entity_id_and_session_index()
    {
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp.example.com",
            SessionIndex = "session123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp.example.com",
            SessionIndex = "session123",
            NameId = "different@example.com", // Different NameId shouldn't matter
            NameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent" // Different format shouldn't matter
        };

        session1.Equals(session2).ShouldBeTrue();
        session1.GetHashCode().ShouldBe(session2.GetHashCode());
    }

    [Fact]
    [Trait("Category", Category)]
    public void equals_should_return_false_for_different_entity_id()
    {
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "session123",
            NameId = "user@example.com"
        };

        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp2.example.com",
            SessionIndex = "session123",
            NameId = "user@example.com"
        };

        session1.Equals(session2).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void equals_should_return_false_for_different_session_index()
    {
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp.example.com",
            SessionIndex = "session123",
            NameId = "user@example.com"
        };

        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp.example.com",
            SessionIndex = "session456",
            NameId = "user@example.com"
        };

        session1.Equals(session2).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void union_should_deduplicate_sessions()
    {
        var list1 = new List<SamlSpSessionData>
        {
            new()
            {
                EntityId = "https://sp1.example.com",
                SessionIndex = "session1",
                NameId = "user@example.com"
            },
            new()
            {
                EntityId = "https://sp2.example.com",
                SessionIndex = "session2",
                NameId = "user@example.com"
            }
        };

        var list2 = new List<SamlSpSessionData>
        {
            new()
            {
                EntityId = "https://sp1.example.com",
                SessionIndex = "session1",
                NameId = "different@example.com" // Duplicate session (different NameId shouldn't matter)
            },
            new()
            {
                EntityId = "https://sp3.example.com",
                SessionIndex = "session3",
                NameId = "user@example.com"
            }
        };

        var result = list1.Union(list2).ToList();

        // Should have 3 unique sessions (sp1/session1, sp2/session2, sp3/session3)
        result.Count.ShouldBe(3);
        result.Count(s => s.EntityId == "https://sp1.example.com").ShouldBe(1);
        result.Count(s => s.EntityId == "https://sp2.example.com").ShouldBe(1);
        result.Count(s => s.EntityId == "https://sp3.example.com").ShouldBe(1);
    }

    [Fact]
    [Trait("Category", Category)]
    public void distinct_should_remove_duplicate_sessions()
    {
        var sessions = new List<SamlSpSessionData>
        {
            new()
            {
                EntityId = "https://sp.example.com",
                SessionIndex = "session1",
                NameId = "user1@example.com"
            },
            new()
            {
                EntityId = "https://sp.example.com",
                SessionIndex = "session1",
                NameId = "user2@example.com" // Duplicate (different NameId)
            },
            new()
            {
                EntityId = "https://sp.example.com",
                SessionIndex = "session2",
                NameId = "user@example.com"
            }
        };

        var result = sessions.Distinct().ToList();

        result.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public void contains_should_work_correctly()
    {
        var sessions = new List<SamlSpSessionData>
        {
            new()
            {
                EntityId = "https://sp.example.com",
                SessionIndex = "session1",
                NameId = "user@example.com"
            }
        };

        var lookupSession = new SamlSpSessionData
        {
            EntityId = "https://sp.example.com",
            SessionIndex = "session1",
            NameId = "different@example.com" // Different NameId shouldn't matter
        };

        sessions.Contains(lookupSession).ShouldBeTrue();
    }
}
