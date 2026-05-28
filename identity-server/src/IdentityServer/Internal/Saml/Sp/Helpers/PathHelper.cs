// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    /// <summary>
    /// Class to help with mapping virtual paths relative to the server.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// Returns the base path of the website or application running. 
        /// </summary>
        public static string BasePath
        {
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// Maps a virtual path to the <c>BasePath</c> of the running appliction.
        /// </summary>
        /// <param name="virtualPath">The virtual path that needs to mapped relative to the server.</param>
        /// <returns>A file path.</returns>
        public static string MapPath(string virtualPath)
        {
            if (virtualPath == null)
            {
                throw new ArgumentNullException(nameof(virtualPath));
            }

            if (!IsWebRootRelative(virtualPath))
            {
                return Path.GetFullPath(virtualPath);
            }


            // Strip until and including the initial /
            virtualPath = virtualPath.Substring(virtualPath.IndexOfAny(new char[] { '/', '\\' }) + 1);

            // Normalize the slashes.
            virtualPath = virtualPath.Replace('/', '\\');
            return Path.Combine(BasePath, virtualPath);
        }

        /// <summary>
        /// Determines if a virtual path is relative or not.
        /// </summary>
        /// <param name="virtualPath">The path that is to be tested.</param>
        /// <returns>True if the path is relative otherwise false.</returns>
        public static bool IsWebRootRelative(string virtualPath)
        {
            if (virtualPath == null)
            {
                throw new ArgumentNullException(nameof(virtualPath));
            }
            if (virtualPath.Length == 0)
            {
                return false;
            }

            if (virtualPath.StartsWith(@"~/", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if a url is relative to current host, excluding protocol-relative addresses
        /// </summary>
        /// <param name="url">The path that is to be tested.</param>
        /// <returns>True if the url is relative otherwise false.</returns>
        public static bool IsLocalWebUrl(string url)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }
            if (IsWebRootRelative(url))
            {
                return true;
            }
            if (url.StartsWith(@"/", StringComparison.Ordinal)
                && !url.StartsWith(@"//", StringComparison.Ordinal)
                && !url.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }
    }
}
