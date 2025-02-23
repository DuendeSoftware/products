using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace DPoPApi.Controllers
{
    [Route("identity")]
    public class IdentityController : ControllerBase
    {
        private readonly ILogger<IdentityController> _logger;

        public IdentityController(ILogger<IdentityController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult Get()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value });
            _logger.LogInformation("claims: {claims}", claims);

            var scheme = Request.GetAuthorizationScheme();
            var proofToken = Request.GetDPoPProofToken();

            return new JsonResult(new { scheme, proofToken, claims });
        }

        [HttpGet("TestNonce")]
        [AllowAnonymous]
        public ActionResult TestNonce()
        {
            var x = Request.GetDPoPProofToken();
            var props = new AuthenticationProperties();
            props.SetDPoPNonce("custom-nonce");

            return Challenge(props);
        }
    }
}