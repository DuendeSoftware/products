// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.IntegrationTests.Common;

public class MessageHandlerWrapper : DelegatingHandler
{
    public HttpResponseMessage Response { get; set; }

    public MessageHandlerWrapper(HttpMessageHandler handler)
        : base(handler)
    {
    }

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CT ct)
    {
        Response = await base.SendAsync(request, ct);
        return Response;
    }
}
