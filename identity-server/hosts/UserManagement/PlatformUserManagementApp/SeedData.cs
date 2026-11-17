// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;

namespace PlatformUserManagementApp;

internal static class SeedData
{
    // Admin credentials — for demo purposes only
    internal const string AdminUserName = "admin@example.com";
    internal const string AdminPassword = "AdminPassword1234!#";
    internal const string AdminEmail = "admin@example.com";

    internal static async Task SeedAsync(IServiceProvider services, Ct ct)
    {
        var userAdmin = services.GetRequiredService<IUserAdmin>();
        var selfService = services.GetRequiredService<IUserAuthenticatorsSelfService>();

        // Check if admin already exists
        if (await userAdmin.Profiles.TryGetAsync(ScimSchema.UserName.Code, AdminUserName, ct) is not null)
        {
            return;
        }

        var schema = await userAdmin.Profiles.GetSchemaAsync(ct);
        var attributes = new AttributeValueCollection(schema);

        attributes.Set(ScimSchema.UserName.Code, AdminUserName);
        attributes.Set(ScimSchema.DisplayName.Code, "Administrator");
        attributes.Set(ScimSchema.Name.Code, (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["givenName"] = "Admin",
            ["familyName"] = "User"
        });
        attributes.Set(ScimSchema.Active.Code, true);

        var profile = await userAdmin.Profiles.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct);
        if (profile is null)
        {
            throw new InvalidOperationException("Failed to create admin user profile.");
        }

        var otpAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create(AdminEmail));
        _ = await userAdmin.Authenticators.TryAddAsync(profile.SubjectId, [otpAddress], [], ct);

        var password = await selfService.ValidatePasswordAsync(profile.SubjectId, AdminPassword, ct);

        if (!await selfService.TrySetPasswordAsync(profile.SubjectId, password, ct))
        {
            throw new InvalidOperationException("Failed to set admin password.");
        }

        // Seed one distinct user so the user list isn't empty
        await SeedSampleUserAsync(userAdmin, ct);
    }

    private static async Task SeedSampleUserAsync(
        IUserAdmin userAdmin,
        Ct ct)
    {
        var schema = await userAdmin.Profiles.GetSchemaAsync(ct);
        var attrs = new AttributeValueCollection(schema);
        //attrs.Set(ScimSchema.UserName.Code, "zoe@example.com");
        attrs.Set(ScimSchema.DisplayName.Code, "Zoe MontThereShire");
        attrs.Set(ScimSchema.Name.Code, (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
        {
            ["formatted"] = "Zoe MontThereShire",
            ["givenName"] = "Zoe",
            ["familyName"] = "MontThereShire"
        });
        attrs.Set(ScimSchema.Title.Code, "Platform Engineer");
        attrs.Set(ScimSchema.UserType.Code, "Employee");
        attrs.Set(ScimSchema.Active.Code, true);

        // Emails (list of complex)
        IReadOnlyList<object> emailList =
        [
            (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
            {
                ["value"] = "zoe@example.com",
                ["type"] = "work",
                ["primary"] = true
            }
        ];
        attrs.Set(ScimSchema.Emails.Code, emailList);

        // Phone numbers (list of complex)
        IReadOnlyList<object> phoneList =
        [
            (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
            {
                ["value"] = "+1-555-0199",
                ["type"] = "work",
                ["primary"] = false
            }
        ];
        attrs.Set(ScimSchema.PhoneNumbers.Code, phoneList);

        // Addresses (list of complex)
        IReadOnlyList<object> addressList =
        [
            (IReadOnlyDictionary<string, object>)new Dictionary<string, object>
            {
                ["locality"] = "Vancouver",
                ["country"] = "CA",
                ["type"] = "work",
                ["primary"] = false
            }
        ];
        attrs.Set(ScimSchema.Addresses.Code, addressList);

        var profile = await userAdmin.Profiles.TryAddAsync(UserSubjectId.New(), attrs.Validate(), ct);
        if (profile is null)
        {
            return;
        }

        var otpAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create("zoe@example.com"));
        _ = await userAdmin.Authenticators.TryAddAsync(profile.SubjectId, [otpAddress], [], ct);
    }
}
