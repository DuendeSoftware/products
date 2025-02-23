// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Net.Http;

namespace IntegrationTests.Common;

public class BrowserClient : HttpClient
{
    public BrowserClient(BrowserHandler browserHandler)
        : base(browserHandler)
    {
        BrowserHandler = browserHandler;
    }

    public BrowserHandler BrowserHandler { get; private set; }

    public bool AllowCookies
    {
        get { return BrowserHandler.AllowCookies; }
        set { BrowserHandler.AllowCookies = value; }
    }
    public bool AllowAutoRedirect
    {
        get { return BrowserHandler.AllowAutoRedirect; }
        set { BrowserHandler.AllowAutoRedirect = value; }
    }
    public int ErrorRedirectLimit
    {
        get { return BrowserHandler.ErrorRedirectLimit; }
        set { BrowserHandler.ErrorRedirectLimit = value; }
    }
    public int StopRedirectingAfter
    {
        get { return BrowserHandler.StopRedirectingAfter; }
        set { BrowserHandler.StopRedirectingAfter = value; }
    }

    internal void RemoveCookie(string uri, string name)
    {
        BrowserHandler.RemoveCookie(uri, name);
    }

    internal System.Net.Cookie GetCookie(string uri, string name)
    {
        return BrowserHandler.GetCookie(uri, name);
    }
}