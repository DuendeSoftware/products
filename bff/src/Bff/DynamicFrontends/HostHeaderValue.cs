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

        return string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase)
               && Port == other.Port;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Host, StringComparer.InvariantCultureIgnoreCase);
        hashCode.Add(Port);
        return hashCode.ToHashCode();
    }

    public static HostHeaderValue Parse(string hostHeaderValue)
    {
        if (string.IsNullOrEmpty(hostHeaderValue))
        {
            throw new ArgumentException("Not a valid host header");
        }


        if (hostHeaderValue.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(hostHeaderValue, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Uri must be an absolute URI.", nameof(hostHeaderValue));
            }

            return Parse(uri);
        }

        if (!hostHeaderValue.Contains(':', StringComparison.OrdinalIgnoreCase))
        {
            return new HostHeaderValue
            {
                Host = hostHeaderValue,
                Port = 443
            };
        }

        var parts = hostHeaderValue.Split(':');
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var port))
            {
                throw new ArgumentException(
                    $"Not a valid host header value. The port number {parts[1]} was not an integer.");
            }

            return new HostHeaderValue
            {
                Host = parts[0],
                Port = port
            };
        }

        throw new ArgumentException($"Invalid host header value: {hostHeaderValue}", nameof(hostHeaderValue));
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
            throw new ArgumentException("Not a valid host header value. Uri must be an absolute URI.", nameof(uri));
        }

        if (uri.Host.Length == 0)
        {
            throw new ArgumentException("Not a valid host header value. Host is empty.", nameof(uri));
        }

        if (uri.PathAndQuery != "/" && uri.PathAndQuery.Length > 0)
        {
            throw new ArgumentException("Not a valid host header value. Uri cannot have a path or query.", nameof(uri));
        }

        return new()
        {
            Host = uri.Host,
            Port = uri.Port
        };
    }

    internal HostString ToHostString() => new(Host, Port);

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

        var requestPort = request.Host.Port ??
                   (string.Equals(request.Scheme, "http", StringComparison.OrdinalIgnoreCase) ? 80 : 443);

        return string.Equals(request.Host.Host, Host, StringComparison.OrdinalIgnoreCase)
               && (requestPort == Port);
    }

    public override string ToString() => $"{Host}:{Port}";


}
