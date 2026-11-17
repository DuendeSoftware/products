// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Import;
using Duende.UserManagement.Profiles;
using Npgsql;
using PlatformImporter = Duende.UserManagement.Import.IUserImporter;

namespace PlatformUserManagementApp.Import;

/// <summary>
/// Imports users from an ASP.NET Identity PostgreSQL database into the Duende Platform user store
/// using the <see cref="PlatformImporter"/> bulk import API.
/// </summary>
internal sealed class AspNetIdentityImporter(
    PlatformImporter platformImporter,
    IUserProfileAdmin profileAdmin,
    NpgsqlDataSource dataSource) : IUserImporter
{
    // Namespace UUID for generating deterministic subject IDs from source user IDs
    private static readonly Guid NamespaceId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public async Task<ImportResult> ImportFromIdentityDbAsync(Ct ct)
    {
        var schema = await profileAdmin.GetSchemaAsync(ct);

        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync(ct);

        // Read all users
        var users = await ReadUsersAsync(conn, ct);

        // Read all claims grouped by user
        var claimsByUser = await ReadClaimsAsync(conn, ct);

        // Read all passkeys grouped by user
        var passkeysByUser = await ReadPasskeysAsync(conn, ct);

        // Map to import records
        var records = new List<UserImportRecord>();

        foreach (var user in users)
        {
            var claims = claimsByUser.GetValueOrDefault(user.Id, []);
            var passkeys = passkeysByUser.GetValueOrDefault(user.Id, []);
            var record = MapUser(user, claims, passkeys, schema);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        if (records.Count == 0)
        {
            return ImportResult.Empty;
        }

        // Import in batches of 50
        var created = 0;
        var overwritten = 0;
        var failed = 0;
        var errors = new List<string>();
        const int batchSize = 50;

        for (var i = 0; i < records.Count; i += batchSize)
        {
            var batch = records.Skip(i).Take(batchSize).ToList();
            var result = await platformImporter.ImportAsync(batch, ct);

            created += result.CreatedCount;
            overwritten += result.UpdatedCount;
            failed += result.FailedCount;

            foreach (var r in result.Results.Where(r => r.Status == UserImportStatus.Failed))
            {
                errors.Add($"User {r.SubjectId}: {r.Error}");
            }
        }

        return new ImportResult(created, overwritten, failed, errors);
    }

    private static UserImportRecord? MapUser(
        IdentityUser user,
        IReadOnlyList<IdentityClaim> claims,
        IReadOnlyList<IdentityPasskey> passkeys,
        IReadOnlyAttributeSchema schema)
    {
        // Build profile attributes from claims
        var attrs = new AttributeValueCollection(schema);

        foreach (var claim in claims)
        {
            var attrName = MapClaimToAttribute(claim.ClaimType);
            if (attrName is not null)
            {
                var name = AttributeCode.Create(attrName);
                if (schema.AttributeDefinitions.ContainsKey(name))
                {
                    attrs.Set(name, claim.ClaimValue);
                }
            }
        }

        // Build name complex attribute from claims
        var nameProps = new Dictionary<string, object>();
        foreach (var claim in claims)
        {
            switch (claim.ClaimType)
            {
                case "given_name":
                    nameProps["givenName"] = claim.ClaimValue;
                    break;
                case "family_name":
                    nameProps["familyName"] = claim.ClaimValue;
                    break;
                case "middle_name":
                    nameProps["middleName"] = claim.ClaimValue;
                    break;
            }
        }

        if (nameProps.Count > 0)
        {
            attrs.Set(ScimSchema.Name.Code, (IReadOnlyDictionary<string, object>)nameProps);
        }

        // Set email attribute (list of complex)
        if (!string.IsNullOrEmpty(user.Email))
        {
            IReadOnlyList<object> emailList =
            [
                (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
                {
                    ["value"] = user.Email,
                    ["type"] = "work",
                    ["primary"] = true
                }
            ];
            attrs.Set(ScimSchema.Emails.Code, emailList);
        }

        // Build phone number from claims (list of complex)
        var phoneClaim = claims.FirstOrDefault(c => c.ClaimType == "phone_number");
        if (phoneClaim is not null)
        {
            var phoneDict = new Dictionary<string, object> { ["value"] = phoneClaim.ClaimValue, ["primary"] = false };
            var phoneTypeClaim = claims.FirstOrDefault(c => c.ClaimType == "phone_number_type");
            if (phoneTypeClaim is not null)
            {
                phoneDict["type"] = phoneTypeClaim.ClaimValue;
            }

            IReadOnlyList<object> phoneList = [(IReadOnlyDictionary<string, object>)phoneDict];
            attrs.Set(ScimSchema.PhoneNumbers.Code, phoneList);
        }

        // Build address from claims (list of complex)
        var addressProps = new Dictionary<string, object>();
        foreach (var claim in claims)
        {
            switch (claim.ClaimType)
            {
                case "street_address":
                    addressProps["streetAddress"] = claim.ClaimValue;
                    break;
                case "locality":
                    addressProps["locality"] = claim.ClaimValue;
                    break;
                case "region":
                    addressProps["region"] = claim.ClaimValue;
                    break;
                case "postal_code":
                    addressProps["postalCode"] = claim.ClaimValue;
                    break;
                case "country":
                    addressProps["country"] = claim.ClaimValue;
                    break;
            }
        }

        if (addressProps.Count > 0)
        {
            _ = addressProps.TryAdd("primary", false);
            IReadOnlyList<object> addressList = [(IReadOnlyDictionary<string, object>)addressProps];
            attrs.Set(ScimSchema.Addresses.Code, addressList);
        }

        // Set username on the authenticator record (not as a SCIM attribute — that's not yet supported)
        if (!string.IsNullOrEmpty(user.UserName))
        {
            attrs.Set(ScimSchema.UserName.Code, user.UserName);
        }

        // Set active
        attrs.Set(ScimSchema.Active.Code, true);

        // Build OTP addresses
        var otpAddresses = new List<OtpAddress>();
        if (!string.IsNullOrEmpty(user.Email) && EmailAddress.TryCreate(user.Email, out var emailAddress))
        {
            otpAddresses.Add(new OtpAddress(OtpChannel.Email, emailAddress));
        }

        // Build password import
        PasswordImport? password = null;
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            password = ConvertPassword(user.PasswordHash);
        }

        // Build passkey imports
        var passkeyImports = passkeys
            .Select(p => new PasskeyImport
            {
                CredentialId = p.CredentialId,
                PublicKeyCose = p.PublicKey,
                Algorithm = ExtractCoseAlgorithm(p.PublicKey),
                SignCount = p.SignCount,
                BackupEligible = p.IsBackupEligible,
                BackedUp = p.IsBackedUp,
                Aaguid = p.Aaguid is { Length: 16 } a ? new Guid(a) : Guid.Empty,
                Name = p.Name ?? "Imported passkey"
            })
            .ToList();

        return new UserImportRecord
        {
            SubjectId = UserSubjectId.Create(GenerateDeterministicGuid(user.Id).ToString()),
            ProfileAttributes = attrs.Count > 0 ? attrs.Validate() : null,
            Authenticators = new AuthenticatorImport
            {
                OtpAddresses = otpAddresses,
                Password = password,
                Passkeys = passkeyImports.Count > 0 ? passkeyImports : null
            }
        };
    }

    private static string? MapClaimToAttribute(string claimType) => claimType switch
    {
        "display_name" => ScimSchema.DisplayName.Code.ToString(),
        "title" => ScimSchema.Title.Code.ToString(),
        "user_type" => ScimSchema.UserType.Code.ToString(),
        "preferred_language" => ScimSchema.PreferredLanguage.Code.ToString(),
        "locale" => ScimSchema.Locale.Code.ToString(),
        "timezone" => ScimSchema.Timezone.Code.ToString(),
        // name, phone_number, and address claims are handled separately as complex types
        _ => null
    };

    private static PasswordImport? ConvertPassword(string aspNetPasswordHash)
    {
        // Store the ASP.NET Identity hash blob as-is under the custom algorithm ID.
        // The AspNetIdentityPasswordHashAlgorithm handles verification, and the platform
        // automatically re-hashes to the preferred algorithm (PBKDF2-SHA512) on first login.
        try
        {
            var bytes = Convert.FromBase64String(aspNetPasswordHash);
            if (bytes.Length == 0 || bytes[0] is not (0x00 or 0x01))
            {
                return null;
            }
        }
        catch (FormatException)
        {
            return null;
        }

        return new PasswordImport(AspNetIdentityPasswordHashAlgorithm.CreateImportData(aspNetPasswordHash));
    }

#pragma warning disable CA5350 // SHA1 is used for deterministic ID generation, not security
    private static Guid GenerateDeterministicGuid(string sourceId)
    {
        var namespaceBytes = NamespaceId.ToByteArray();
        var sourceBytes = System.Text.Encoding.UTF8.GetBytes(sourceId);
        var combined = new byte[namespaceBytes.Length + sourceBytes.Length];
        namespaceBytes.CopyTo(combined, 0);
        sourceBytes.CopyTo(combined, namespaceBytes.Length);

        var hash = SHA1.HashData(combined);

        return new Guid(
            BitConverter.ToInt32(hash, 0),
            BitConverter.ToInt16(hash, 4),
            (short)((BitConverter.ToInt16(hash, 6) & 0x0FFF) | 0x4000),
            (byte)((hash[8] & 0x3F) | 0x80),
            hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
    }
#pragma warning restore CA5350

    private static async Task<List<IdentityUser>> ReadUsersAsync(NpgsqlConnection conn, Ct ct)
    {
        var users = new List<IdentityUser>();
        await using var cmd = new NpgsqlCommand("""
            SELECT "Id", "UserName", "Email", "PasswordHash"
            FROM "AspNetUsers"
            ORDER BY "Id"
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            users.Add(new IdentityUser
            {
                Id = reader.GetString(0),
                UserName = reader.IsDBNull(1) ? null : reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return users;
    }

    private static async Task<Dictionary<string, List<IdentityClaim>>> ReadClaimsAsync(NpgsqlConnection conn, Ct ct)
    {
        var claims = new Dictionary<string, List<IdentityClaim>>();
        await using var cmd = new NpgsqlCommand("""
            SELECT "UserId", "ClaimType", "ClaimValue"
            FROM "AspNetUserClaims"
            ORDER BY "UserId"
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var userId = reader.GetString(0);
            var claim = new IdentityClaim
            {
                ClaimType = reader.GetString(1),
                ClaimValue = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            };

            if (!claims.TryGetValue(userId, out var list))
            {
                list = [];
                claims[userId] = list;
            }

            list.Add(claim);
        }

        return claims;
    }

    private sealed record IdentityUser
    {
        public required string Id { get; init; }
        public string? UserName { get; init; }
        public string? Email { get; init; }
        public string? PasswordHash { get; init; }
    }

    private sealed record IdentityClaim
    {
        public required string ClaimType { get; init; }
        public required string ClaimValue { get; init; }
    }

    private static async Task<Dictionary<string, List<IdentityPasskey>>> ReadPasskeysAsync(NpgsqlConnection conn, Ct ct)
    {
        var passkeys = new Dictionary<string, List<IdentityPasskey>>();
        await using var cmd = new NpgsqlCommand("""
            SELECT "UserId", "CredentialId", "Data"
            FROM "AspNetUserPasskeys"
            ORDER BY "UserId"
            """, conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var userId = reader.GetString(0);
            var credentialId = (byte[])reader.GetValue(1);
            var dataJson = reader.GetString(2);
            var data = JsonSerializer.Deserialize<IdentityPasskeyData>(dataJson);
            if (data is null)
            {
                continue;
            }

            var passkey = new IdentityPasskey
            {
                CredentialId = credentialId,
                PublicKey = data.PublicKey ?? [],
                Name = data.Name,
                SignCount = data.SignCount,
                IsBackupEligible = data.IsBackupEligible,
                IsBackedUp = data.IsBackedUp,
                Aaguid = data.Aaguid
            };

            if (!passkeys.TryGetValue(userId, out var list))
            {
                list = [];
                passkeys[userId] = list;
            }

            list.Add(passkey);
        }

        return passkeys;
    }

    /// <summary>
    /// Extracts the COSE algorithm identifier from a COSE public key.
    /// The algorithm is stored as key label 3 in the COSE key map.
    /// Falls back to -7 (ES256) if extraction fails.
    /// </summary>
    private static int ExtractCoseAlgorithm(byte[] coseKey)
    {
        try
        {
            var reader = new CborReader(coseKey);
            _ = reader.ReadStartMap();
            while (reader.PeekState() != CborReaderState.EndMap)
            {
                var label = reader.ReadInt32();
                if (label == 3) // COSE key algorithm label
                {
                    return reader.ReadInt32();
                }

                reader.SkipValue();
            }
        }
        catch
        {
            // Fall through to default
        }

        return -7; // ES256 default
    }

    private sealed record IdentityPasskey
    {
        public required byte[] CredentialId { get; init; }
        public required byte[] PublicKey { get; init; }
        public string? Name { get; init; }
        public uint SignCount { get; init; }
        public bool IsBackupEligible { get; init; }
        public bool IsBackedUp { get; init; }
        public byte[]? Aaguid { get; init; }
    }

    private sealed class IdentityPasskeyData
    {
        public byte[]? PublicKey { get; set; }
        public string? Name { get; set; }
        public uint SignCount { get; set; }
        public bool IsBackupEligible { get; set; }
        public bool IsBackedUp { get; set; }
        public byte[]? Aaguid { get; set; }
    }
}
