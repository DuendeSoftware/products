// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Extensions for Resource
/// </summary>
public static class ResourceExtensions
{
    extension(ResourceValidationResult resourceValidationResult)
    {
        /// <summary>
        /// Returns the collection of scope values that are required.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetRequiredScopeValues()
        {
            var names = resourceValidationResult.Resources.IdentityResources.Where(x => x.Required).Select(x => x.Name).ToList();
            names.AddRange(resourceValidationResult.Resources.ApiScopes.Where(x => x.Required).Select(x => x.Name));

            var values = resourceValidationResult.ParsedScopes.Where(x => names.Contains(x.ParsedName)).Select(x => x.RawValue);
            return values;
        }
    }

    extension(Resources resources)
    {
        /// <summary>
        /// Converts to scope names.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> ToScopeNames()
        {
            var names = resources.IdentityResources.Select(x => x.Name).ToList();
            names.AddRange(resources.ApiScopes.Select(x => x.Name));
            if (resources.OfflineAccess)
            {
                names.Add(IdentityServerConstants.StandardScopes.OfflineAccess);
            }

            return names;
        }

        /// <summary>
        /// Finds the IdentityResource that matches the scope.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public IdentityResource FindIdentityResourcesByScope(string name)
        {
            var q = from id in resources.IdentityResources
                    where id.Name == name
                    select id;
            return q.FirstOrDefault();
        }

        /// <summary>
        /// Finds the API resources that contain the scope.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public IEnumerable<ApiResource> FindApiResourcesByScope(string name)
        {
            var q = from api in resources.ApiResources
                    where api.Scopes != null && api.Scopes.Contains(name)
                    select api;
            return q.ToArray();
        }

        /// <summary>
        /// Finds the API scope.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public ApiScope FindApiScope(string name)
        {
            var q = from scope in resources.ApiScopes
                    where scope.Name == name
                    select scope;
            return q.FirstOrDefault();
        }

        internal Resources FilterEnabled()
        {
            if (resources == null)
            {
                return new Resources();
            }

            return new Resources(
                resources.IdentityResources.Where(x => x.Enabled),
                resources.ApiResources.Where(x => x.Enabled),
                resources.ApiScopes.Where(x => x.Enabled))
            {
                OfflineAccess = resources.OfflineAccess
            };
        }
    }

    extension(IEnumerable<ApiResource> apiResources)
    {
        internal ICollection<string> FindMatchingSigningAlgorithms()
        {
            var apis = apiResources.ToList();

            if (IEnumerableExtensions.IsNullOrEmpty(apis))
            {
                return new List<string>();
            }

            // only one API resource request, forward the allowed signing algorithms (if any)
            if (apis.Count == 1)
            {
                return apis.First().AllowedAccessTokenSigningAlgorithms;
            }

            var allAlgorithms = apis.Where(r => r.AllowedAccessTokenSigningAlgorithms.Count > 0).Select(r => r.AllowedAccessTokenSigningAlgorithms).ToList();

            // resources need to agree on allowed signing algorithms
            if (allAlgorithms.Count > 0)
            {
                var allowedAlgorithms = IntersectLists(allAlgorithms);

                if (allowedAlgorithms.Any())
                {
                    return allowedAlgorithms.ToHashSet();
                }

                throw new InvalidOperationException("Signing algorithms requirements for requested resources are not compatible.");
            }

            return new List<string>();
        }
    }

    extension(IEnumerable<string> list)
    {
        internal bool AreValidResourceIndicatorFormat(ILogger logger)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (!item.IsUri())
                    {
                        logger.LogDebug("Resource indicator {resource} is not a valid URI.", item);
                        return false;
                    }

                    if (item.Contains('#', StringComparison.InvariantCulture))
                    {
                        logger.LogDebug("Resource indicator {resource} must not contain a fragment component.", item);
                        return false;
                    }
                }
            }

            return true;
        }
    }

    private static IEnumerable<T> IntersectLists<T>(IEnumerable<IEnumerable<T>> lists) => lists.Aggregate((l1, l2) => l1.Intersect(l2));

}
