// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using Duende.IdentityServer.Validation;
using Duende.IdentityModel;
using Xunit;

namespace UnitTests.Extensions;

public class ValidatedAuthorizeRequestExtensionsTests
{
    [Fact]
    public void GetAcrValues_should_return_snapshot_of_values()
    {
        var request = new ValidatedAuthorizeRequest()
        {
            Raw = new System.Collections.Specialized.NameValueCollection()
        };
        request.AuthenticationContextReferenceClasses.Add("a");
        request.AuthenticationContextReferenceClasses.Add("b");
        request.AuthenticationContextReferenceClasses.Add("c");

        var acrs = request.GetAcrValues();
        foreach(var acr in acrs)
        {
            request.RemoveAcrValue(acr);
        }
    }

    [Fact]
    [Obsolete]
    public void ToOptimizedFullDictionary_should_return_dictionary_with_array_for_repeated_keys_when_request_objects_are_used()
    {
        var request = new ValidatedAuthorizeRequest()
        {
            Raw = new System.Collections.Specialized.NameValueCollection
            {
                { OidcConstants.AuthorizeRequest.Request, "Request object here" },
                { OidcConstants.AuthorizeRequest.Resource, "Resource1" },
                { OidcConstants.AuthorizeRequest.Resource, "Resource2" },
            }
        };

        var res = request.ToOptimizedFullDictionary();

        Assert.Equal(2, res[OidcConstants.AuthorizeRequest.Resource].Length);
    }

    [Theory]
    [Obsolete]
    [InlineData(OidcConstants.TokenRequest.ClientAssertion)]
    [InlineData(OidcConstants.TokenRequest.ClientSecret)]
    public void ToOptimizedFullDictionary_should_filter_client_credentials(string filteredClaimType)
    {
        var request = new ValidatedAuthorizeRequest()
        {
            Raw = new System.Collections.Specialized.NameValueCollection
            {
                { filteredClaimType, "" },
            }
        };

        var result = request.ToOptimizedFullDictionary();

        Assert.Empty(result);
    }
}