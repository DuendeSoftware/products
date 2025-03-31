// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.ResponseHandling;

namespace Duende.IdentityServer.Models;

internal static class AuthorizeResponseExtensions
{
    public static NameValueCollection ToNameValueCollection(this AuthorizeResponse response, IdentityServerOptions options)
    {
        var collection = new NameValueCollection();

        if (response.IsError)
        {
            if (response.Error.IsPresent())
            {
                collection.Add("error", response.Error);
            }
            if (response.ErrorDescription.IsPresent())
            {
                collection.Add("error_description", response.ErrorDescription);
            }
        }
        else
        {
            if (response.Code.IsPresent())
            {
                collection.Add("code", response.Code);
            }

            if (response.IdentityToken.IsPresent())
            {
                collection.Add("id_token", response.IdentityToken);
            }

            if (response.AccessToken.IsPresent())
            {
                collection.Add("access_token", response.AccessToken);
                collection.Add("token_type", "Bearer");
                collection.Add("expires_in", response.AccessTokenLifetime.ToString());

                if (response.Scope.IsPresent())
                {
                    collection.Add("scope", response.Scope);
                }
            }
        }

        if (response.State.IsPresent())
        {
            collection.Add("state", response.State);
        }

        if (response.SessionState.IsPresent())
        {
            collection.Add("session_state", response.SessionState);
        }

        if (response.Issuer.IsPresent())
        {
            if (options.EmitIssuerIdentificationResponseParameter)
            {
                collection.Add("iss", response.Issuer);
            }
        }

        return collection;
    }
}
