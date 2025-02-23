// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;
using Shouldly;
using Duende.IdentityModel;
using UnitTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
using Duende.IdentityServer.Services;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;

namespace UnitTests.Endpoints.Results;

public class EventTests
{

    [Fact]
    public void UnhandledExceptionEventCanCallToString()
    {
        try
        {
            throw new InvalidOperationException("Boom");
        }
        catch (Exception ex)
        {
            var unhandledExceptionEvent = new UnhandledExceptionEvent(ex);

            var s = unhandledExceptionEvent.ToString();

            s.ShouldNotBeNullOrEmpty();
        }
    }
}