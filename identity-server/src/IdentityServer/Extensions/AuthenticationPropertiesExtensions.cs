// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions for AuthenticationProperties
/// </summary>
public static class AuthenticationPropertiesExtensions
{
    internal const string SessionIdKey = "session_id";
    internal const string ClientListKey = "client_list";

    /// <summary>
    /// Gets the user's session identifier.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static string GetSessionId(this AuthenticationProperties properties) => properties?.Items.TryGetValue(SessionIdKey, out var value) == true ? value : null;

    /// <summary>
    /// Sets the user's session identifier.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="sid">The session id</param>
    /// <returns></returns>
    public static void SetSessionId(this AuthenticationProperties properties, string sid) => properties.Items[SessionIdKey] = sid;

    /// <summary>
    /// Gets the list of client ids the user has signed into during their session.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetClientList(this AuthenticationProperties properties)
    {
        if (properties?.Items.TryGetValue(ClientListKey, out var value) == true)
        {
            return DecodeList(value);
        }

        return [];
    }

    /// <summary>
    /// Removes the list of client ids.
    /// </summary>
    /// <param name="properties"></param>
    public static void RemoveClientList(this AuthenticationProperties properties) => properties?.Items.Remove(ClientListKey);

    /// <summary>
    /// Sets the list of client ids.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="clientIds"></param>
    public static void SetClientList(this AuthenticationProperties properties, IEnumerable<string> clientIds)
    {
        var value = EncodeList(clientIds);
        if (value == null)
        {
            properties.Items.Remove(ClientListKey);
        }
        else
        {
            properties.Items[ClientListKey] = value;
        }
    }

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="clientId"></param>
    public static void AddClientId(this AuthenticationProperties properties, string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        var clients = properties.GetClientList();
        if (!clients.Contains(clientId))
        {
            properties.SetClientList(clients.Append(clientId));
        }
    }


    private static IEnumerable<string> DecodeList(string value)
    {
        if (value.IsPresent())
        {
            var bytes = Base64Url.DecodeFromChars(value);
            value = Encoding.UTF8.GetString(bytes);
            return ObjectSerializer.FromString<string[]>(value);
        }

        return Enumerable.Empty<string>();
    }

    private static string EncodeList(IEnumerable<string> list)
    {
        if (list != null && list.Any())
        {
            var value = ObjectSerializer.ToString(list);
            var bytes = Encoding.UTF8.GetBytes(value);
            value = Base64Url.EncodeToString(bytes);
            return value;
        }

        return null;
    }
}
