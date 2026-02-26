// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml.Linq;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal interface ISamlResultSerializer<T>
{
    XElement Serialize(T toSerialize);
}
