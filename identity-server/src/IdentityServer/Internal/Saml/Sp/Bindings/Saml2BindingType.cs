// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Bindings
{
    /// <summary>
    /// Saml2 binding types.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1027:MarkEnumsWithFlags", Justification = "Might do that in the future, but not right now")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
    internal enum Saml2BindingType
    {
        /// <summary>
        /// The http redirect binding according to saml bindings section 3.4
        /// </summary>
        HttpRedirect = 1,

        /// <summary>
        /// The http post binding according to saml bindings section 3.5
        /// </summary>
        HttpPost = 2,
    }
}
