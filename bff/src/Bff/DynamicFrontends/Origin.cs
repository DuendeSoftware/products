// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Describes the origin of a request. 
/// </summary>
public sealed record Origin : IEquatable<HttpRequest>
{
    public static Origin Parse(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"Can't create origin from '{origin}'");
        }

        return Parse(uri);
    }

    public static Origin? ParseOrDefault(string? origin)
    {
        if (string.IsNullOrEmpty(origin))
        {
            return null;
        }
        try
        {
            return Parse(origin);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    public static Origin Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return new()
        {
            Scheme = uri.Scheme,
            Host = uri.Host,
            Port = uri.Port
        };
    }

    internal HostString ToHostString() => new(Host, Port);

    public required string Scheme { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; } = 443;

    public bool Equals(HttpRequest? request)
    {
        if (request == null)
        {
            return false;
        }

        return string.Equals(request.Host.Host, Host, StringComparison.OrdinalIgnoreCase)
               && (request.Host.Port == null || request.Host.Port == Port)
               && string.Equals(request.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => $"{Scheme}://{Host}:{Port}";

    public Uri ToUri() => new UriBuilder
    {
        Scheme = Scheme,
        Host = Host,
        Port = Port
    }.Uri;
}
