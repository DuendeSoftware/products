// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

using System;

namespace Duende.IdentityServer.EntityFramework.Entities;

public class ServerSideSession
{
    public long Id { get; set; }
    public string Key { get; set; }
    public string Scheme { get; set; }
    public string SubjectId { get; set; }
    public string SessionId { get; set; }
    public string DisplayName { get; set; }
    public DateTime Created { get; set; }
    public DateTime Renewed { get; set; }
    public DateTime? Expires { get; set; }
    public string Data { get; set; }
}
