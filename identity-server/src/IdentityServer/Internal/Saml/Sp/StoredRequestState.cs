// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Text;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Microsoft.IdentityModel.Tokens.Saml2;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Stored data for pending requests.
    /// </summary>
    internal class StoredRequestState
    {
        /// <summary>
        /// Start of the cookie name for state preservation.
        /// </summary>
        public const string CookieNameBase = "Saml2.";

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="idp">The EntityId of the IDP the request was sent to</param>
        /// <param name="returnUrl">The Url to redirect back to after a succesful login</param>
        /// <param name="messageId">ID of the SAML message, used to match InResponseTo</param>
        /// <param name="relayData">Aux data that can be stored across the authentication request.</param>
        public StoredRequestState(
            EntityId idp,
            Uri returnUrl,
            Saml2Id messageId,
            IDictionary<string, string> relayData)
        {
            Idp = idp;
            ReturnUrl = returnUrl;
            MessageId = messageId;
            RelayData = relayData;
        }

        /// <summary>
        /// The IDP the request was sent to
        /// </summary>
        public EntityId Idp { get; }

        /// <summary>
        /// The Url to redirect back to after a succesful login
        /// </summary>
        public Uri ReturnUrl { get; }

        /// <summary>
        /// Message id of the originating Saml message. Should match InResponseTo
        /// in the response.
        /// </summary>
        public Saml2Id MessageId { get; }

        /// <summary>
        /// Aux data that need to be preserved across the authentication call.
        /// </summary>
        public IDictionary<string, string> RelayData { get; }

        /// <summary>
        /// Get a serialized representation of the data.
        /// </summary>
        /// <returns>Serialized data</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(Idp?.Id ?? "");
                w.Write(ReturnUrl?.OriginalString ?? "");
                w.Write(MessageId?.Value ?? "");
                var hasRelayData = RelayData != null;
                w.Write(hasRelayData);
                if (hasRelayData)
                {
                    w.Write(RelayData.Count);
                    foreach (var kv in RelayData)
                    {
                        w.Write(kv.Key);
                        var hasValue = kv.Value != null;
                        w.Write(hasValue);
                        if (hasValue)
                        {
                            w.Write(kv.Value);
                        }
                    }
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Ctor that restores a StoredRequestState object from serialized
        /// representation.
        /// </summary>
        /// <param name="data">data buffer</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public StoredRequestState(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms, Encoding.UTF8, true))
            {
                var idp = r.ReadString();
                if (!string.IsNullOrEmpty(idp))
                {
                    Idp = new EntityId(idp);
                }

                var returnUrl = r.ReadString();
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    ReturnUrl = new Uri(returnUrl, UriKind.RelativeOrAbsolute);
                }

                var messageId = r.ReadString();
                if (!string.IsNullOrEmpty(messageId))
                {
                    MessageId = new Saml2Id(messageId);
                }

                var hasRelayData = r.ReadBoolean();
                if (hasRelayData)
                {
                    RelayData = new Dictionary<string, string>();
                    var count = r.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var key = r.ReadString();
                        string value = null;
                        var hasValue = r.ReadBoolean();
                        if (hasValue)
                        {
                            value = r.ReadString();
                        }
                        RelayData[key] = value;
                    }
                }
            }
        }
    }
}
