// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.DynamicFrontends;

public static class BffAuthenticationSchemes
{
    public static readonly Scheme BffOpenIdConnect = Scheme.Parse("duende-bff-oidc");
    public static readonly Scheme BffDefault = Scheme.Parse("duende-bff-default");
}
