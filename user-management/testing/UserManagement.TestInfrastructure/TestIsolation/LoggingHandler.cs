// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// A <see cref="DelegatingHandler"/> that logs HTTP requests and responses to
/// the xUnit test output so traffic is visible in test logs.
/// <para>
/// Request:  <c>==> GET https://1-saml.dev.localhost:5001/connect/authorize</c><br/>
/// Response: <c>&lt;== 302 from GET https://1-saml.dev.localhost:5001/connect/authorize</c>
/// </para>
/// </summary>
public sealed class LoggingHandler : DelegatingHandler
{
    private readonly ITestOutputHelper _output;
    private readonly string _prefix;

    public LoggingHandler(HttpMessageHandler innerHandler, ITestOutputHelper output, string prefix)
        : base(innerHandler)
    {
        _output = output;
        _prefix = prefix;
    }

    public LoggingHandler(HttpMessageHandler innerHandler, ITestOutputHelper output)
        : this(innerHandler, output, "http")
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _output.WriteLine($"[{_prefix}] ==> {request.Method} {request.RequestUri}");

        var response = await base.SendAsync(request, cancellationToken);

        _output.WriteLine(
            $"[{_prefix}] <== {(int)response.StatusCode} {response.StatusCode} from {request.Method} {request.RequestUri}");

        return response;
    }
}
