// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// A middleware that adds a <see cref="MockHttpActivityFeature"/> to the pipeline and makes it available for
/// inspection.
/// </summary>
public class MockTestActivityMiddleware
{
    public Activity CapturedActivity { get; private set; }

    public Task Handle(HttpContext context, RequestDelegate next)
    {
        var activity = new Activity("TestRequest");
        var feature = new MockHttpActivityFeature { Activity = activity };
        context.Features.Set<IHttpActivityFeature>(feature);

        activity.Start();
        CapturedActivity = activity;

        try
        {
            return next(context);
        }
        finally
        {
            activity.Stop();
        }
    }
}
