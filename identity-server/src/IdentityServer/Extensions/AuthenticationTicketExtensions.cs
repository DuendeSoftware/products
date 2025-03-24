// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Extensions;

/// <summary>
///  Extension methods for AuthenticationTicket
/// </summary>
public static class AuthenticationTicketExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Extracts a subject identifier
    /// </summary>
    public static string GetSubjectId(this AuthenticationTicket ticket)
    {
        return ticket.Principal.FindFirst(JwtClaimTypes.Subject)?.Value ??
               throw new InvalidOperationException("Missing subject id for principal in authentication ticket.");
    }

    /// <summary>
    /// Extracts the session ID
    /// </summary>
    public static string GetSessionId(this AuthenticationTicket ticket)
    {
        return ticket.Properties.GetSessionId() ??
            throw new InvalidOperationException("Missing session id for principal in authentication ticket.");
    }

    /// <summary>
    /// Extracts the display name
    /// </summary>
    public static string GetDisplayName(this AuthenticationTicket ticket, string displayNameClaimType)
    {
        return string.IsNullOrWhiteSpace(displayNameClaimType) ?
            null : ticket.Principal.FindFirst(displayNameClaimType)?.Value;
    }

    /// <summary>
    /// Gets the issuer
    /// </summary>
    public static string GetIssuer(this AuthenticationTicket ticket)
    {
        ticket.Properties.Items.TryGetValue(JwtClaimTypes.Issuer, out var value);
        return value;
    }

    /// <summary>
    /// Sets a issuer
    /// </summary>
    public static void SetIssuer(this AuthenticationTicket ticket, string issuer)
    {
        ticket.Properties.Items[JwtClaimTypes.Issuer] = issuer;
    }


    /// <summary>
    /// Extracts the issuance time
    /// </summary>
    public static DateTime GetIssued(this AuthenticationTicket ticket)
    {
        return ticket.Properties.IssuedUtc?.UtcDateTime ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Extracts the expiration time
    /// </summary>
    public static DateTime? GetExpiration(this AuthenticationTicket ticket)
    {
        return ticket.Properties.ExpiresUtc?.UtcDateTime;
    }


    /// <summary>
    /// Serializes and AuthenticationTicket to a string
    /// </summary>
    public static string Serialize(this AuthenticationTicket ticket, IDataProtector protector)
    {
        var data = new AuthenticationTicketLite
        {
            Scheme = ticket.AuthenticationScheme,
            User = ticket.Principal.ToClaimsPrincipalLite(),
            Items = ticket.Properties.Items
        };

        var payload = JsonSerializer.Serialize(data, JsonOptions);
        payload = protector.Protect(payload);

        var envelope = new AuthenticationTicketEnvelope { Version = 1, Payload = payload };
        var value = JsonSerializer.Serialize(envelope, JsonOptions);

        return value;
    }

    /// <summary>
    /// Deserializes a UserSession's Ticket to an AuthenticationTicket
    /// </summary>
    public static AuthenticationTicket Deserialize(this ServerSideSession session, IDataProtector protector, ILogger logger)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<AuthenticationTicketEnvelope>(session.Ticket, JsonOptions);
            if (envelope == null)
            {
                return null;
            }

            if (envelope.Version != 1)
            {
                logger.LogWarning("Deserializing AuthenticationTicket envelope found incorrect version for key {key}.", session.Key);
                return null;
            }

            string payload;
            try
            {
                payload = protector.Unprotect(envelope.Payload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to unprotect AuthenticationTicket payload for key {key}", session.Key);
                return null;
            }

            var ticket = JsonSerializer.Deserialize<AuthenticationTicketLite>(payload, JsonOptions);
            if (ticket == null)
            {
                return null;
            }

            var user = ticket.User.ToClaimsPrincipal();
            var properties = new AuthenticationProperties(ticket.Items);

            // this allows us to extend the session from the DB column rather than from the payload
            if (session.Expires.HasValue)
            {
                properties.ExpiresUtc = new DateTimeOffset(session.Expires.Value, TimeSpan.Zero);
            }
            else
            {
                properties.ExpiresUtc = null;
            }

            // similar to the expires update above, this is needed so that the ticket will have the 
            // right issued timestamp when the cookie handler performs its sliding logic
            properties.IssuedUtc = new DateTimeOffset(session.Renewed, TimeSpan.Zero);

            return new AuthenticationTicket(user, properties, ticket.Scheme);
        }
        catch (Exception ex)
        {
            // failed deserialize
            logger.LogError(ex, "Failed to deserialize UserSession payload for key {key}", session.Key);
        }

        return null;
    }

    /// <summary>
    /// Serialization friendly AuthenticationTicket
    /// </summary>
    public class AuthenticationTicketLite
    {
        /// <summary>
        /// The scheme
        /// </summary>
        public string Scheme { get; init; } = default!;

        /// <summary>
        /// The user
        /// </summary>
        public ClaimsPrincipalLite User { get; init; } = default!;

        /// <summary>
        /// The items
        /// </summary>
        public IDictionary<string, string> Items { get; init; } = default!;
    }

    /// <summary>
    /// Envelope for serialized data
    /// </summary>
    public class AuthenticationTicketEnvelope
    {
        /// <summary>
        /// Version
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Payload
        /// </summary>
        public string Payload { get; init; } = default!;
    }
}
