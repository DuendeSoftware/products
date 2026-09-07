// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;

#pragma warning disable 1591

namespace Duende.IdentityServer.Validation;

public static class ValidatedAuthorizeRequestExtensions
{
    extension(ValidatedAuthorizeRequest request)
    {
        public void RemovePrompt()
        {
            var suppress = new StringBuilder();
            if (request.PromptModes.Contains(OidcConstants.PromptModes.Login))
            {
                suppress.Append(OidcConstants.PromptModes.Login);
            }
            if (request.PromptModes.Contains(OidcConstants.PromptModes.SelectAccount))
            {
                if (suppress.Length > 0)
                {
                    suppress.Append(' ');
                }
                suppress.Append(OidcConstants.PromptModes.SelectAccount);
            }
            if (request.PromptModes.Contains(OidcConstants.PromptModes.Create))
            {
                if (suppress.Length > 0)
                {
                    suppress.Append(' ');
                }
                suppress.Append(OidcConstants.PromptModes.Create);
            }

            request.Raw.Add(Constants.ProcessedPrompt, suppress.ToString());
            request.PromptModes = request.PromptModes.Except(new[] {
                OidcConstants.PromptModes.Login,
                OidcConstants.PromptModes.SelectAccount,
                OidcConstants.PromptModes.Create
            }).ToArray();
        }

        public void RemoveMaxAge()
        {
            if (request.MaxAge.HasValue)
            {
                request.Raw.Add(Constants.ProcessedMaxAge, request.MaxAge.Value.ToString(CultureInfo.InvariantCulture));
                request.MaxAge = null;
            }
        }

        public string GetPrefixedAcrValue(string prefix)
        {
            var value = request.AuthenticationContextReferenceClasses
                .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.Ordinal));

            if (value != null)
            {
                value = value.Substring(prefix.Length);
            }

            return value;
        }

        public void RemovePrefixedAcrValue(string prefix)
        {
            request.AuthenticationContextReferenceClasses.RemoveAll(acr => acr.StartsWith(prefix, StringComparison.Ordinal));
            var acr_values = request.AuthenticationContextReferenceClasses.ToSpaceSeparatedString();
            if (acr_values.IsPresent())
            {
                request.Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
            }
            else
            {
                request.Raw.Remove(OidcConstants.AuthorizeRequest.AcrValues);
            }
        }

        public string GetIdP() => request.GetPrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);

        public void RemoveIdP() => request.RemovePrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);

        public string GetTenant() => request.GetPrefixedAcrValue(Constants.KnownAcrValues.Tenant);

        public IEnumerable<string> GetAcrValues() => request
                .AuthenticationContextReferenceClasses
                .Where(acr => !Constants.KnownAcrValues.All.Any(well_known => acr.StartsWith(well_known, StringComparison.Ordinal)))
                .Distinct()
                .ToArray();

        public void RemoveAcrValue(string value)
        {
            request.AuthenticationContextReferenceClasses.RemoveAll(x => x.Equals(value, StringComparison.Ordinal));
            var acr_values = request.AuthenticationContextReferenceClasses.ToSpaceSeparatedString();
            if (acr_values.IsPresent())
            {
                request.Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
            }
            else
            {
                request.Raw.Remove(OidcConstants.AuthorizeRequest.AcrValues);
            }
        }

        public void AddAcrValue(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            request.AuthenticationContextReferenceClasses.Add(value);
            var acr_values = request.AuthenticationContextReferenceClasses.ToSpaceSeparatedString();
            request.Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
        }

        public string GenerateSessionStateValue()
        {
            if (request == null)
            {
                return null;
            }

            if (!request.IsOpenIdRequest)
            {
                return null;
            }

            if (request.SessionId == null)
            {
                return null;
            }

            if (request.ClientId.IsMissing())
            {
                return null;
            }

            if (request.RedirectUri.IsMissing())
            {
                return null;
            }

            var clientId = request.ClientId;
            var sessionId = request.SessionId;
            var salt = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);

            var uri = new Uri(request.RedirectUri);
            var origin = uri.Scheme + "://" + uri.Host;
            if (!uri.IsDefaultPort)
            {
                origin += ":" + uri.Port;
            }

            var bytes = Encoding.UTF8.GetBytes(clientId + origin + sessionId + salt);
            var hash = SHA256.HashData(bytes);

            return Base64Url.EncodeToString(hash) + "." + salt;
        }

        private NameValueCollection ToOptimizedRawValues()
        {
            if (request.Raw.AllKeys.Contains(OidcConstants.AuthorizeRequest.Request))
            {
                // if we already have a request object in the URL, then we can filter out the duplicate entries in the Raw collection
                var collection = new NameValueCollection();
                foreach (var key in request.Raw.AllKeys)
                {
                    // https://openid.net/specs/openid-connect-core-1_0.html#JWTRequests
                    // requires client id and response type to always be in URL
                    if (key == OidcConstants.AuthorizeRequest.ClientId ||
                        key == OidcConstants.AuthorizeRequest.ResponseType ||
                        request.RequestObjectValues.All(x => x.Type != key))
                    {
                        foreach (var value in request.Raw.GetValues(key))
                        {
                            collection.Add(key, value);
                        }
                    }
                }

                return collection;
            }

            return request.Raw;
        }

        public string ToOptimizedQueryString() => request.ToOptimizedRawValues().ToQueryString();

        [Obsolete("This method is obsolete and will be removed in a future version.")]
        public IDictionary<string, string[]> ToOptimizedFullDictionary()
        {
            var collection = request.ToOptimizedRawValues();

            // Filter client authentication out of the dictionary that we're building
            // It's possible for a PAR request to include client auth and we don't want
            // that to cause these values to get passed into the (obsolete) authorization
            // parameters message store, where they might get logged or otherwise stored
            // insecurely.
            collection.Remove(OidcConstants.TokenRequest.ClientAssertion);
            collection.Remove(OidcConstants.TokenRequest.ClientSecret);

            return collection.ToFullDictionary();
        }
    }
}
