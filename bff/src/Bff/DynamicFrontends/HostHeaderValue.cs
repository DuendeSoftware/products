// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Describes a host header value (scheme, host, port) and can be used to compare against an HttpRequest.
///
/// Note, normally host header values do not include the scheme, but we need this to be able to match
/// on default ports as well. Technically, this class is an "Origin", but due to conflicts with the concepts
/// of origins in CORS HostHeaderValue.
/// </summary>
public sealed record HostHeaderValue : IEquatable<HttpRequest>
{
    public bool Equals(HostHeaderValue? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Scheme, other.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
               && Port == other.Port;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Scheme, StringComparer.InvariantCultureIgnoreCase);
        hashCode.Add(Host, StringComparer.InvariantCultureIgnoreCase);
        hashCode.Add(Port);
        return hashCode.ToHashCode();
    }

    public static HostHeaderValue Parse(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            throw new UriFormatException($"Can't create origin from '{origin}'");
        }

        return Parse(uri);
    }

    public static HostHeaderValue? ParseOrDefault(string? origin)
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

    public static HostHeaderValue Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Uri must be an absolute URI.");
        }

        return new()
        {
            Scheme = uri.Scheme,
            Host = uri.Host,
            Port = uri.Port
        };
    }

    internal HostString ToHostString() => new(Host, Port);

    /// <summary>
    /// the scheme of a http request. Usually "http" or "https".
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// The hostname of the host. 
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The port number. When using default ports, this will be 80 for http and 443 for https.
    /// </summary>
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
