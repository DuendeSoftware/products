// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Licensing.v2;

internal interface IProtocolRequestCounter
{
    void Increment();
    uint RequestCount { get; }
}