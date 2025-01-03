// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.Bff.Tests.TestFramework
{
    public class TestBrowserClient : HttpClient
    {
        class CookieHandler : DelegatingHandler
        {
            public CookieContainer CookieContainer { get; } = new CookieContainer();
            public Uri CurrentUri { get; private set; }
            public HttpResponseMessage LastResponse { get; private set; }

            public CookieHandler(HttpMessageHandler next)
                : base(next)
            {
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CurrentUri = request.RequestUri;
                string cookieHeader = CookieContainer.GetCookieHeader(request.RequestUri);
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    request.Headers.Add("Cookie", cookieHeader);
                }

                var response = await base.SendAsync(request, cancellationToken);

                if (response.Headers.Contains("Set-Cookie"))
                {
                    var responseCookieHeader = string.Join(",", response.Headers.GetValues("Set-Cookie"));
                    CookieContainer.SetCookies(request.RequestUri, responseCookieHeader);
                }

                LastResponse = response;

                return response;
            }
        }

        private CookieHandler _handler;
        
        public CookieContainer CookieContainer => _handler.CookieContainer;
        public Uri CurrentUri => _handler.CurrentUri;
        public HttpResponseMessage LastResponse => _handler.LastResponse;

        public TestBrowserClient(HttpMessageHandler handler)
            : this(new CookieHandler(handler))
        {
        }

        private TestBrowserClient(CookieHandler handler)
            : base(handler)
        {
            _handler = handler;
        }

        public void RemoveCookie(string name)
        {
            RemoveCookie(CurrentUri.ToString(), name);
        }
        public void RemoveCookie(string uri, string name)
        {
            var cookie = CookieContainer.GetCookies(new Uri(uri)).FirstOrDefault(x => x.Name == name);
            if (cookie != null)
            {
                cookie.Expired = true;
            }
        }
    }
}