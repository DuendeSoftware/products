// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetIdentityApp.Pages.Account;

[Authorize]
[IgnoreAntiforgeryToken]
public sealed class EnrollPasskeyModel(
    UserManager<Data.ApplicationUser> userManager) : PageModel
{
    private const string RelyingPartyId = "localhost";
    private const string RelyingPartyName = "ASP.NET Identity Sample";
    private const string ChallengeCookieName = "__Host-PasskeyChallenge";

    public string? ReturnUrl { get; set; }
    public List<string> ErrorMessages { get; set; } = [];

    public IActionResult OnGet(string? returnUrl)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return Page();
    }

    public async Task<IActionResult> OnPostOptionsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return BadRequest("User not found.");
        }

        var userName = User.Identity?.Name ?? userId;

        var challenge = new byte[32];
        RandomNumberGenerator.Fill(challenge);

        // Store challenge in a cookie for verification
        var challengeBase64 = Convert.ToBase64String(challenge);
        Response.Cookies.Append(ChallengeCookieName, challengeBase64, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddMinutes(5)
        });

        var options = new
        {
            challenge = Base64UrlEncode(challenge),
            rp = new { id = RelyingPartyId, name = RelyingPartyName },
            user = new
            {
                id = Base64UrlEncode(Encoding.UTF8.GetBytes(userId)),
                name = userName,
                displayName = userName
            },
            pubKeyCredParams = new[]
            {
                new { type = "public-key", alg = -7 },  // ES256
                new { type = "public-key", alg = -257 } // RS256
            },
            authenticatorSelection = new
            {
                residentKey = "preferred",
                userVerification = "preferred"
            },
            timeout = 60000,
            attestation = "none"
        };

        return new JsonResult(options);
    }

    public async Task<IActionResult> OnPostRegisterAsync([FromBody] PasskeyRegistrationResponse response)
    {
        if (!Request.Cookies.TryGetValue(ChallengeCookieName, out var challengeBase64) ||
            string.IsNullOrEmpty(challengeBase64))
        {
            return BadRequest("Challenge expired.");
        }

        Response.Cookies.Delete(ChallengeCookieName);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return BadRequest("User not found.");
        }

        try
        {
            // Decode the client data JSON
            var clientDataJson = Convert.FromBase64String(PadBase64(response.Response.ClientDataJSON.Replace('-', '+').Replace('_', '/')));
            var clientData = JsonSerializer.Deserialize<ClientDataJson>(clientDataJson);

            if (clientData?.Type != "webauthn.create")
            {
                return BadRequest("Invalid client data type.");
            }

            // Verify origin per WebAuthn §7.1 step 7
            var requestOrigin = $"{Request.Scheme}://{Request.Host}";
            if (!string.Equals(clientData.Origin, requestOrigin, StringComparison.Ordinal))
            {
                return BadRequest("Invalid origin.");
            }

            // Verify challenge
            var expectedChallenge = challengeBase64;
            var receivedChallenge = Convert.ToBase64String(Convert.FromBase64String(PadBase64(clientData.Challenge.Replace('-', '+').Replace('_', '/'))));
            if (!string.Equals(expectedChallenge, receivedChallenge, StringComparison.Ordinal))
            {
                return BadRequest("Challenge mismatch.");
            }

            // Decode attestation object to get credential ID and public key
            var attestationObjectBytes = Convert.FromBase64String(PadBase64(response.Response.AttestationObject.Replace('-', '+').Replace('_', '/')));
            var credentialId = Convert.FromBase64String(PadBase64(response.RawId.Replace('-', '+').Replace('_', '/')));

            // Extract public key and flags from authenticator data (simplified — no attestation verification)
            var (publicKey, flags) = ExtractPublicKeyAndFlagsFromAttestationObject(attestationObjectBytes);
            if (publicKey.Length == 0)
            {
                return BadRequest("Failed to extract public key from attestation object.");
            }

            var passkeyInfo = new UserPasskeyInfo(
                credentialId: credentialId,
                publicKey: publicKey,
                createdAt: DateTimeOffset.UtcNow,
                signCount: 0,
                transports: [],
                isUserVerified: (flags & 0x04) != 0,
                isBackupEligible: (flags & 0x08) != 0,
                isBackedUp: (flags & 0x10) != 0,
                attestationObject: attestationObjectBytes,
                clientDataJson: clientDataJson);

            var result = await userManager.AddOrUpdatePasskeyAsync(user, passkeyInfo);
            if (!result.Succeeded)
            {
                return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest($"Registration failed: {ex.Message}");
        }
    }

    private static (byte[] PublicKey, byte Flags) ExtractPublicKeyAndFlagsFromAttestationObject(byte[] attestationObject)
    {
        // Parse CBOR-encoded attestation object to extract authData
        // authData contains the credential public key in COSE format
        // This is a simplified extraction — returns the raw authData public key bytes
        try
        {
            var reader = new CborReader(attestationObject);
            _ = reader.ReadStartMap();

            byte[]? authData = null;
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                var key = reader.ReadTextString();
                if (key == "authData")
                {
                    authData = reader.ReadByteString();
                }
                else
                {
                    reader.SkipValue();
                }
            }

            if (authData is null || authData.Length < 37)
            {
                return ([], 0);
            }

            // authData structure: rpIdHash(32) + flags(1) + signCount(4) + attestedCredentialData(variable)
            // attestedCredentialData: aaguid(16) + credIdLen(2) + credId(credIdLen) + publicKey(COSE)
            var flags = authData[32];
            var offset = 37; // skip rpIdHash + flags + signCount
            if (authData.Length <= offset + 18)
            {
                return ([], flags);
            }

            offset += 16; // skip aaguid
            var credIdLen = (authData[offset] << 8) | authData[offset + 1];
            offset += 2 + credIdLen;

            // Return the COSE public key bytes and flags
            return (authData[offset..], flags);
        }
        catch
        {
            return ([], 0);
        }
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string PadBase64(string base64)
    {
        var padding = base64.Length % 4;
        return padding == 0 ? base64 : base64 + new string('=', 4 - padding);
    }

    public sealed class PasskeyRegistrationResponse
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("rawId")] public string RawId { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("response")] public AttestationResponse Response { get; set; } = new();
    }

    public sealed class AttestationResponse
    {
        [JsonPropertyName("attestationObject")] public string AttestationObject { get; set; } = string.Empty;
        [JsonPropertyName("clientDataJSON")] public string ClientDataJSON { get; set; } = string.Empty;
    }

    private sealed class ClientDataJson
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("challenge")] public string Challenge { get; set; } = string.Empty;
        [JsonPropertyName("origin")] public string? Origin { get; set; }
    }
}
