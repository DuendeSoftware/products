// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.UserManagement.Authentication.QrCodes;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;

namespace TotpAuthentication;

public class TotpAuthenticatorService(IUserProfileSelfService selfService)
{
    private const string IssuerName = "Duende.Persona.TOTP";

    public async Task<TotpAuthenticatorSetup> GetOrCreateAuthenticatorSetupAsync(UserProfile profile, string email, Ct ct)
    {
        var keyBase32 = (string?)profile.Attributes.GetValueOrDefault(UserAttributes.AuthenticatorKey)?.UntypedValue;

        if (string.IsNullOrEmpty(keyBase32))
        {
            // Generate new TOTP key
            keyBase32 = PlainBytesTotpKey.New().EncodeToBase32();
            var userUpdate = profile.ToUpdate();
            userUpdate.Set(UserAttributes.AuthenticatorKey, keyBase32);
            await selfService.TryUpdateAsync(profile.SubjectId, userUpdate.Validate(), ct);
        }

        var sharedKey = FormatKeyToHumanReadable(keyBase32);
        var authenticatorUri = GenerateQrCodeUri(email, keyBase32);

        // --- QR Code Generation using Duende's built-in QR encoder ---
        //
        // QrEncoder.Encode() converts text to a QR symbol. It auto-selects the
        // smallest QR version (1-40) that fits the data. You can override this
        // with QrEncodeOptions { Version = N } if you need a specific size.
        //
        // QrEncodeOptions.EccLevel controls error correction capacity:
        //   QrEccLevel.L  ~7%  recovery (smallest QR, most data capacity)
        //   QrEccLevel.M  ~15% recovery (default - good general-purpose choice)
        //   QrEccLevel.Q  ~25% recovery
        //   QrEccLevel.H  ~30% recovery (largest QR, best damage tolerance)
        //
        // For TOTP URIs, Medium (M) is a good balance of scannability and size.
        var qrSymbol = QrEncoder.Encode(authenticatorUri, new QrEncodeOptions { EccLevel = QrEccLevel.M });

        // Renderers available:
        //   QrSvgRenderer.Render()  -> string  SVG (scalable, inline-able in HTML, no dependencies)
        //   QrPngRenderer.Render()  -> byte[]  PNG (use Convert.ToBase64String() for data URIs)
        //
        // moduleSize controls the pixel size of each QR module:
        //   moduleSize=1 -> compact (good for SVG since it scales via CSS/viewBox)
        //   moduleSize=4-8 -> good for PNG raster output
        //
        // quietZoneModules (default 4) is the white border per ISO 18004.
        var qrCodeSvg = QrSvgRenderer.Render(qrSymbol);

        return new TotpAuthenticatorSetup(sharedKey, authenticatorUri, qrCodeSvg);
    }

    /// <summary>
    /// Formats a Base32 key for human readability.
    /// Input (raw): JBSWY3DPEHPK3PXP
    /// Output (formatted): jbsw y3dp ehpk 3pxp
    /// </summary>
    private static string FormatKeyToHumanReadable(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private static string GenerateQrCodeUri(string email, string unformattedKey) =>
        TotpAuthenticatorUri.Generate(IssuerName, email, PlainBytesTotpKey.DecodeFromBase32(unformattedKey));
}

public record TotpAuthenticatorSetup(string SharedKeyFormatted, string AuthenticatorUri, string QrCodeSvg);
