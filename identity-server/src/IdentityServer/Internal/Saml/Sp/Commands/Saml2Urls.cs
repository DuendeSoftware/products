// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
namespace Duende.IdentityServer.Internal.Saml.Sp.Commands
{
    /// <summary>
    /// The urls of Saml2 that are used in various messages.
    /// </summary>
    internal class Saml2Urls
    {
        /// <summary>
        /// Resolve the urls for Saml2 from an http request and options.
        /// </summary>
        /// <param name="request">Request to get application root url from.</param>
        /// <param name="options">Options to get module path and (optional) notification hooks from.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SignInUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LogoutUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "Saml2Url")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AssertionConsumerServiceUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ApplicationUrl")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public Saml2Urls(HttpRequestData request, IOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var publicOrigin = options.Notifications.GetPublicOrigin(request) ?? options.SPOptions.PublicOrigin ?? request.ApplicationUrl;
            Init(publicOrigin, options.SPOptions.ModulePath);

            options.SPOptions.Logger.WriteVerbose("Expanded Saml2Url"
                + "\n  AssertionConsumerServiceUrl: " + AssertionConsumerServiceUrl
                + "\n  SignInUrl: " + SignInUrl
                + "\n  LogoutUrl: " + LogoutUrl
                + "\n  ApplicationUrl: " + ApplicationUrl);
        }

        /// <summary>
        /// Creates the urls for Saml2 based on the complete base Url
        /// the application and the Saml2 base module path.
        /// </summary>
        /// <param name="applicationUrl">The full Url to the root of the application.</param>
        /// <param name="modulePath">Path of module, starting with / and ending without.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads"
            , Justification = "Incorrect warning. modulePath isn't a string representation of a Uri")]
        public Saml2Urls(Uri applicationUrl, string modulePath)
        {
            if (applicationUrl == null)
            {
                throw new ArgumentNullException(nameof(applicationUrl));
            }

            if (modulePath == null)
            {
                throw new ArgumentNullException(nameof(modulePath));
            }

            Init(applicationUrl, modulePath);
        }

        /// <summary>
        /// Creates the urls for Saml2 based on the given full urls
        /// for assertion consumer service and sign-in
        /// </summary>
        /// <param name="assertionConsumerServiceUrl">The full Url for the Assertion Consumer Service.</param>
        /// <param name="signInUrl">The full Url for sign-in.</param>
        /// <param name="applicationUrl">The full Url for the application root.</param>
        public Saml2Urls(Uri assertionConsumerServiceUrl, Uri signInUrl, Uri applicationUrl)
        {
            if (signInUrl == null)
            {
                throw new ArgumentNullException(nameof(signInUrl));
            }

            AssertionConsumerServiceUrl = assertionConsumerServiceUrl;
            SignInUrl = signInUrl;
            ApplicationUrl = applicationUrl;
        }

        void Init(Uri publicOrigin, string modulePath)
        {
            if (!modulePath.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("modulePath should start with /.");
            }

            if (!publicOrigin.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                var publicOriginBuilder = new UriBuilder(publicOrigin)
                {
                    Path = publicOrigin.AbsolutePath.TrimEnd('/') + "/"
                };

                publicOrigin = publicOriginBuilder.Uri;
            }

            var saml2Root = new Uri(publicOrigin, modulePath.TrimStart('/') + "/");

            AssertionConsumerServiceUrl = new Uri(saml2Root, CommandFactory.AcsCommandName);
            SignInUrl = new Uri(saml2Root, CommandFactory.SignInCommandName);
            ApplicationUrl = publicOrigin;
            LogoutUrl = new Uri(saml2Root, CommandFactory.LogoutCommandName);
        }

        /// <summary>
        /// The full url of the assertion consumer service.
        /// </summary>
        public Uri AssertionConsumerServiceUrl { get; private set; }

        /// <summary>
        /// The full url of the signin command, which is also the response 
        /// location for idp discovery.
        /// </summary>
        public Uri SignInUrl { get; private set; }

        /// <summary>
        /// The full url of the application root. Used as default redirect
        /// location after logout.
        /// </summary>
        public Uri ApplicationUrl { get; internal set; }

        /// <summary>
        /// The full url of the logout command.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Uri LogoutUrl { get; internal set; }
    }
}
