// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Mvc;

namespace DPoPApi.Controllers;

[Route("identity")]
public class IdentityController : ControllerBase
{
    private readonly ILogger<IdentityController> _logger;

    public IdentityController(ILogger<IdentityController> logger) => _logger = logger;

    [HttpGet]
    public ActionResult Get()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        _logger.LogInformation("claims: {claims}", claims);

        var scheme = GetAuthorizationScheme(Request);
        var proofToken = GetDPoPProofToken(Request);

        return new JsonResult(new { scheme, proofToken, claims });
    }

    private static string GetAuthorizationScheme(HttpRequest request) =>
        request.Headers.Authorization.FirstOrDefault()?.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)[0];

    private static string GetDPoPProofToken(HttpRequest request) =>
        request.Headers[OidcConstants.HttpHeaders.DPoP].FirstOrDefault();
}
