// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Saml.Models;
using Microsoft.AspNetCore.Authentication;

namespace UnitTests.Extensions;

public class AuthenticationPropertiesExtensionsTests
{
    private const string Category = "AuthenticationPropertiesExtensions";

    [Fact]
    [Trait("Category", Category)]
    public void get_saml_session_list_when_no_sessions_should_return_empty()
    {
        var properties = new AuthenticationProperties();

        var sessions = properties.GetSamlSessionList();

        sessions.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void add_saml_session_when_new_session_should_add_to_list()
    {
        var properties = new AuthenticationProperties();
        var session = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(session);

        var sessions = properties.GetSamlSessionList().ToList();
        sessions.ShouldHaveSingleItem();
        sessions[0].EntityId.ShouldBe("https://sp1.example.com");
        sessions[0].SessionIndex.ShouldBe("abc123");
        sessions[0].NameId.ShouldBe("user@example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void add_saml_session_when_multiple_sessions_should_add_all_to_list()
    {
        var properties = new AuthenticationProperties();
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };
        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp2.example.com",
            SessionIndex = "def456",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(session1);
        properties.AddSamlSession(session2);

        var sessions = properties.GetSamlSessionList().ToList();
        sessions.Count.ShouldBe(2);
        sessions.ShouldContain(s => s.EntityId == "https://sp1.example.com");
        sessions.ShouldContain(s => s.EntityId == "https://sp2.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void add_saml_session_when_duplicate_entity_id_should_update_session()
    {
        var properties = new AuthenticationProperties();
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };
        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com", // Same EntityId
            SessionIndex = "abc123", // Same SessionIndex (reused)
            NameId = "updated@example.com", // Updated NameId
            NameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent"
        };

        properties.AddSamlSession(session1);
        properties.AddSamlSession(session2);

        var sessions = properties.GetSamlSessionList().ToList();
        sessions.ShouldHaveSingleItem();
        sessions[0].EntityId.ShouldBe("https://sp1.example.com");
        sessions[0].SessionIndex.ShouldBe("abc123");
        sessions[0].NameId.ShouldBe("updated@example.com");
        sessions[0].NameIdFormat.ShouldBe("urn:oasis:names:tc:SAML:2.0:nameid-format:persistent");
    }

    [Fact]
    [Trait("Category", Category)]
    public void remove_saml_session_when_session_exists_should_remove_it()
    {
        var properties = new AuthenticationProperties();
        var session1 = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };
        var session2 = new SamlSpSessionData
        {
            EntityId = "https://sp2.example.com",
            SessionIndex = "def456",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(session1);
        properties.AddSamlSession(session2);
        properties.RemoveSamlSession("https://sp1.example.com");

        var sessions = properties.GetSamlSessionList().ToList();
        sessions.ShouldHaveSingleItem();
        sessions[0].EntityId.ShouldBe("https://sp2.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void remove_saml_session_when_session_does_not_exist_should_do_nothing()
    {
        var properties = new AuthenticationProperties();
        var session = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(session);
        properties.RemoveSamlSession("https://sp2.example.com");

        var sessions = properties.GetSamlSessionList().ToList();
        sessions.ShouldHaveSingleItem();
        sessions[0].EntityId.ShouldBe("https://sp1.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public void saml_session_data_serialization_roundtrip_should_preserve_data()
    {
        var properties = new AuthenticationProperties();
        var originalSession = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123def456",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(originalSession);

        // Retrieve the session
        var retrievedSessions = properties.GetSamlSessionList().ToList();

        retrievedSessions.ShouldHaveSingleItem();
        var retrievedSession = retrievedSessions[0];
        retrievedSession.EntityId.ShouldBe(originalSession.EntityId);
        retrievedSession.SessionIndex.ShouldBe(originalSession.SessionIndex);
        retrievedSession.NameId.ShouldBe(originalSession.NameId);
        retrievedSession.NameIdFormat.ShouldBe(originalSession.NameIdFormat);
    }

    [Fact]
    [Trait("Category", Category)]
    public void set_saml_session_list_when_empty_list_should_remove_key()
    {
        var properties = new AuthenticationProperties();
        var session = new SamlSpSessionData
        {
            EntityId = "https://sp1.example.com",
            SessionIndex = "abc123",
            NameId = "user@example.com",
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress"
        };

        properties.AddSamlSession(session);
        properties.SetSamlSessionList(Array.Empty<SamlSpSessionData>());

        properties.Items.ContainsKey("saml_session_list").ShouldBeFalse();
        properties.GetSamlSessionList().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void session_index_generation_should_be_unique()
    {
        var sessionIndex1 = Guid.NewGuid().ToString("N");
        var sessionIndex2 = Guid.NewGuid().ToString("N");

        sessionIndex1.ShouldNotBe(sessionIndex2);
        sessionIndex1.Length.ShouldBe(32); // GUID without hyphens
        sessionIndex2.Length.ShouldBe(32);
    }
}
