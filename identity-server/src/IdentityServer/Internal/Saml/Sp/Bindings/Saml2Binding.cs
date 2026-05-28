// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;
namespace Duende.IdentityServer.Internal.Saml.Sp.Bindings
{
    /// <summary>
    /// The result of a Saml2Binding.UnBind.
    /// </summary>
    internal class UnbindResult
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="data">The data payload</param>
        /// <param name="relayState">The associated relay state.</param>
        /// <param name="trustLevel">Level of trust that can be put in data.
        /// Does not care about any signature included in the data.</param>
        public UnbindResult(XmlElement data, string relayState, TrustLevel trustLevel)
        {
            Data = data;
            RelayState = relayState;
            TrustLevel = trustLevel;
        }

        /// <summary>
        /// The data payload.
        /// </summary>
        public XmlElement Data { get; }

        /// <summary>
        /// The associated relay state, if any. Otherwise null.
        /// </summary>
        public string RelayState { get; }

        /// <summary>
        /// Trust level indicating how much the message contents can be
        /// trusted.
        /// </summary>
        public TrustLevel TrustLevel { get; }
    }

    /// <summary>
    /// Abstract base for all Saml2Bindings that binds a message to a specific
    /// kind of transport.
    /// </summary>
    internal abstract class Saml2Binding
    {
        /// <summary>
        /// Uri identifier of the HTTP-POST binding.
        /// </summary>
        public static readonly Uri HttpPostUri = new Uri("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST");

        /// <summary>
        /// Uri identifier of the HTTP-Redirect binding.
        /// </summary>
        public static readonly Uri HttpRedirectUri = new Uri("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect");

        /// <summary>
        /// Uri identifier of the Discovery Response SAML extension.
        /// </summary>
        public static readonly Uri DiscoveryResponseUri = new Uri("urn:oasis:names:tc:SAML:profiles:SSO:idp-discovery-protocol");

        /// <summary>
        /// Uri identifier of the SOAP binding.
        /// </summary>
        public static readonly Uri SoapUri = new Uri("urn:oasis:names:tc:SAML:2.0:bindings:SOAP");

        /// <summary>
        /// Binds a message to a http response.
        /// </summary>
        /// <param name="message">Message to bind.</param>
        /// <param name="logger">Logger to log use, can be null.</param>
        /// <returns>CommandResult.</returns>
        public virtual CommandResult Bind(ISaml2Message message, ILoggerAdapter logger)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Binds a message to an http response.
        /// </summary>
        /// <typeparam name="TMessage">Type of the message.</typeparam>
        /// <param name="message">Message to bind</param>
        /// <param name="logger">Logger to log use, can be null.</param>
        /// <param name="xmlCreatedNotification">Notification to call for modification of XDocument, can be null.</param>
        /// <returns>CommandResult.</returns>
        public virtual CommandResult Bind<TMessage>(
            TMessage message, ILoggerAdapter logger, Action<TMessage, XDocument, Saml2BindingType> xmlCreatedNotification)
            where TMessage : ISaml2Message
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Binds a message to a http response.
        /// </summary>
        /// <param name="message">Message to bind.</param>
        /// <returns>CommandResult.</returns>
        public CommandResult Bind(ISaml2Message message)
        {
            return Bind(message, null);
        }

        /// <summary>
        /// Extracts a message out of the current HttpRequest.
        /// </summary>
        /// <param name="request">Current HttpRequest.</param>
        /// <param name="options">Options, used to look up certificate information
        /// in bindings that validate signatures. If set to null, the returned 
        /// result will have TrustLevel.None.</param>
        /// <returns>Extracted message.</returns>
        public virtual UnbindResult Unbind(HttpRequestData request, IOptions options)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks if the binding can extract a message out of the current
        /// http request.
        /// </summary>
        /// <param name="request">HttpRequest to check for message.</param>
        /// <returns>True if the binding supports the current request.</returns>
        protected internal abstract bool CanUnbind(HttpRequestData request);

        private static readonly IDictionary<Saml2BindingType, Saml2Binding> bindings =
            new Dictionary<Saml2BindingType, Saml2Binding>()
            {
                { Saml2BindingType.HttpRedirect, new Saml2RedirectBinding() },
                { Saml2BindingType.HttpPost, new Saml2PostBinding() }
            };

        /// <summary>
        /// Get a cached binding instance that supports the requested type.
        /// </summary>
        /// <param name="binding">Type of binding to get</param>
        /// <returns>A derived class instance that supports the requested binding.</returns>
        public static Saml2Binding Get(Saml2BindingType binding)
        {
            try
            {
                return bindings[binding];
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    "{0} is not a valid value for the Saml2BindingType enum. Have you forgotten to configure a binding for your identity provider?",
                    binding), ex);
            }
        }

        /// <summary>
        /// Get a cached binding instance that can handle the current request.
        /// </summary>
        /// <param name="request">Current HttpRequest</param>
        /// <returns>A derived class instance that supports the requested binding,
        /// or null if no binding supports the current request.</returns>
        public static Saml2Binding Get(HttpRequestData request)
        {
            return bindings.FirstOrDefault(b => b.Value.CanUnbind(request)).Value;
        }

        private readonly static IDictionary<Uri, Saml2BindingType> bindingTypeMap = new Dictionary<Uri, Saml2BindingType>()
        {
            { HttpRedirectUri, Saml2BindingType.HttpRedirect },
            { HttpPostUri, Saml2BindingType.HttpPost }
        };

        private readonly static IDictionary<Saml2BindingType, Uri> bindingUriMap = new Dictionary<Saml2BindingType, Uri>()
        {
            { Saml2BindingType.HttpPost, HttpPostUri }
        };

        /// <summary>
        /// Gets the Saml2BindingType enum value for a Saml2Binding type uri, where the
        /// uri should be one specified in the standard.
        /// </summary>
        /// <param name="uri">Uri for the binding.</param>
        /// <returns>Binding type enum value.</returns>
        /// <exception cref="ArgumentException">If the uri doesn't correspond to a known binding.</exception>
        public static Saml2BindingType UriToSaml2BindingType(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (bindingTypeMap.TryGetValue(uri, out Saml2BindingType bindingType))
            {
                return bindingType;
            }

            var msg = string.Format(CultureInfo.InvariantCulture, "Unknown Saml2 Binding Uri \"{0}\".", uri);
            throw new ArgumentException(msg);
        }

        /// <summary>
        /// Gets the Uri for a Saml2BindingType.
        /// </summary>
        /// <param name="type">Saml2BindingType</param>
        /// <returns>Uri constant for the speicified Binding Type</returns>
        /// <exception cref="ArgumentException">If the type is not mapped.</exception>
        public static Uri Saml2BindingTypeToUri(Saml2BindingType type)
        {
            if (bindingUriMap.TryGetValue(type, out Uri uri))
            {
                return uri;
            }

            var msg = string.Format(CultureInfo.InvariantCulture, "Unknown Saml2 Binding Type \"{0}\".", type);
            throw new ArgumentException(msg);
        }
    }
}
