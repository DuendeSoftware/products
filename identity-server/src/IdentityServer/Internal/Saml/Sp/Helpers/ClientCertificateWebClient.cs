// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    /// <summary>
    /// A WebClient implementation that will add a list of client 
    /// certificates to the requests it makes.
    /// </summary>
    internal class ClientCertificateWebClient : WebClient
    {
        private readonly IEnumerable<X509Certificate2> certificates;
        /// <summary>
        /// Register the certificate to be used for this requets.
        /// </summary>
        /// <param name="certificates">Certificates to offer to server</param>
        public ClientCertificateWebClient(IEnumerable<X509Certificate2> certificates)
        {
            this.certificates = certificates;
        }
        /// <summary>
        /// Override the base class to add the certificate 
        /// to the reuqest before returning it.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            if (certificates != null)
            {
                foreach (var c in certificates)
                {
                    request.ClientCertificates.Add(c);
                }
            }
            return request;
        }
    }
}
