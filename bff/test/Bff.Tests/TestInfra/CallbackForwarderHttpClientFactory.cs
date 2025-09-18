// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Yarp.ReverseProxy.Forwarder;

namespace Duende.Bff.Tests.TestInfra;

public class CallbackForwarderHttpClientFactory(Func<ForwarderHttpClientContext, HttpMessageInvoker> callback)
    : IForwarderHttpClientFactory
{
    public Func<ForwarderHttpClientContext, HttpMessageInvoker> CreateInvoker { get; set; } = callback;

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => CreateInvoker.Invoke(context);
}
