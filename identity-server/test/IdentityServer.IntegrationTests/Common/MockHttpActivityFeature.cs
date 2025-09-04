// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace IntegrationTests.Common;

/// <summary>
/// A simple implementation of <see cref="IHttpActivityFeature"/> for testing purposes.
/// </summary>
public class MockHttpActivityFeature : IHttpActivityFeature
{
    public Activity Activity { get; set; } = new("TestRequest");
}
