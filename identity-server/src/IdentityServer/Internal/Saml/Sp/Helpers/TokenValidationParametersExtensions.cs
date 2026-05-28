// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Reflection;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    internal static class TokenValidationParametersExtensions
    {
        private static readonly PropertyInfo requireAudienceProperty = typeof(TokenValidationParameters).GetProperty("RequireAudience");

        public static TokenValidationParameters SetRequireAudience(this TokenValidationParameters tokenValidationParameters, bool value)
        {
            if (requireAudienceProperty != null)
            {
                requireAudienceProperty.SetValue(tokenValidationParameters, value);
            }

            return tokenValidationParameters;
        }
    }
}
