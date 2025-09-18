// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace Duende.Bff.Tests.TestInfra;

public interface IHttpClient<TSelf> where TSelf : IHttpClient<TSelf>
{
    public static abstract TSelf Build(RedirectHandler handler, CookieContainer cookies);
}
