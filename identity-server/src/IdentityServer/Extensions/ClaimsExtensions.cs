// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using System.Text.Json;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Extensions;

internal static class ClaimsExtensions
{
    public static Dictionary<string, object> ToClaimsDictionary(this IEnumerable<Claim> claims)
    {
        var d = new Dictionary<string, object>();

        if (claims == null)
        {
            return d;
        }

        var distinctClaims = claims.Distinct(new ClaimComparer());

        foreach (var claim in distinctClaims)
        {
            if (!d.TryGetValue(claim.Type, out var value))
            {
                d.Add(claim.Type, GetValue(claim));
            }
            else
            {
                if (value is List<object> list)
                {
                    list.Add(GetValue(claim));
                }
                else
                {
                    _ = d.Remove(claim.Type);
                    d.Add(claim.Type, new List<object> { value, GetValue(claim) });
                }
            }
        }

        return d;
    }

    private static object GetValue(Claim claim)
    {
        if (claim.ValueType is ClaimValueTypes.Integer or ClaimValueTypes.Integer32)
        {
            if (int.TryParse(claim.Value, out var value))
            {
                return value;
            }
        }

        if (claim.ValueType == ClaimValueTypes.Integer64)
        {
            if (long.TryParse(claim.Value, out var value))
            {
                return value;
            }
        }

        if (claim.ValueType == ClaimValueTypes.Double)
        {
            if (double.TryParse(claim.Value, out var value))
            {
                return value;
            }
        }

        if (claim.ValueType == ClaimValueTypes.Boolean)
        {
            if (bool.TryParse(claim.Value, out var value))
            {
                return value;
            }
        }

        // Ignore case here so that we also match System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json ("JSON")
        if (claim.ValueType.Equals(IdentityServerConstants.ClaimValueTypes.Json, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(claim.Value);
            }
            catch { }
        }

        return claim.Value;
    }
}
