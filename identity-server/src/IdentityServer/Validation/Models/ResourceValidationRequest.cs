// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using System;
using System.Collections.Generic;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the request to validate scopes and resource indicators for a client.
/// </summary>
public class ResourceValidationRequest
{
    /// <summary>
    /// The client.
    /// </summary>
    public Client Client { get; set; } = default!;

    /// <summary>
    /// The requested scope values.
    /// </summary>
    public IEnumerable<string> Scopes { get; set; } = default!;

    /// <summary>
    /// The requested resource indicators.
    /// </summary>
    public IEnumerable<string>? ResourceIndicators { get; set; }
}
