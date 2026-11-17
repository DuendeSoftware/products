// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace AspNetIdentityApp.Data;

internal static class SeedData
{
    private const string DefaultPassword = "Pass123$";

    private static readonly IReadOnlyList<SeedUser> Users =
    [
        new("alice.johnson", "alice.johnson@example.com", "Alice", "Johnson", null, "Ms.", "Employee", "en-US", "en_US", "America/New_York", "+1-555-0101", "work", "123 Main St", "Springfield", "IL", "62701", "US"),
        new("bob.smith", "bob.smith@example.com", "Bob", "Smith", "James", "Mr.", "Employee", "en-US", "en_US", "America/Chicago", "+1-555-0102", "work", "456 Oak Ave", "Chicago", "IL", "60601", "US"),
        new("carol.white", "carol.white@example.com", "Carol", "White", null, "Dr.", "Manager", "en-GB", "en_GB", "Europe/London", "+44-20-7946-0103", "work", "10 Downing St", "London", null, "SW1A 2AA", "GB"),
        new("david.brown", "david.brown@example.com", "David", "Brown", "Lee", "Mr.", "Employee", "en-US", "en_US", "America/Los_Angeles", "+1-555-0104", "mobile", "789 Pine Rd", "Los Angeles", "CA", "90001", "US"),
        new("emma.davis", "emma.davis@example.com", "Emma", "Davis", null, "Ms.", "Contractor", "fr-FR", "fr_FR", "Europe/Paris", "+33-1-23-45-67-89", "work", "1 Rue de la Paix", "Paris", null, "75001", "FR"),
        new("frank.miller", "frank.miller@example.com", "Frank", "Miller", "Thomas", "Mr.", "Employee", "de-DE", "de_DE", "Europe/Berlin", "+49-30-12345678", "work", "Unter den Linden 1", "Berlin", null, "10117", "DE"),
        new("grace.wilson", "grace.wilson@example.com", "Grace", "Wilson", null, "Ms.", "Manager", "en-US", "en_US", "America/New_York", "+1-555-0107", "work", "321 Elm St", "Boston", "MA", "02101", "US"),
        new("henry.moore", "henry.moore@example.com", "Henry", "Moore", "Arthur", "Mr.", "Employee", "en-AU", "en_AU", "Australia/Sydney", "+61-2-9876-5432", "work", "1 George St", "Sydney", "NSW", "2000", "AU"),
        new("isabella.taylor", "isabella.taylor@example.com", "Isabella", "Taylor", null, "Ms.", "Contractor", "es-ES", "es_ES", "Europe/Madrid", "+34-91-123-4567", "mobile", "Calle Mayor 1", "Madrid", null, "28001", "ES"),
        new("james.anderson", "james.anderson@example.com", "James", "Anderson", "Robert", "Mr.", "Employee", "en-CA", "en_CA", "America/Toronto", "+1-416-555-0110", "work", "100 King St W", "Toronto", "ON", "M5X 1A9", "CA"),
        new("karen.thomas", "karen.thomas@example.com", "Karen", "Thomas", null, "Dr.", "Manager", "en-US", "en_US", "America/Chicago", "+1-555-0111", "work", "500 Michigan Ave", "Chicago", "IL", "60611", "US"),
        new("liam.jackson", "liam.jackson@example.com", "Liam", "Jackson", "Patrick", "Mr.", "Employee", "en-IE", "en_IE", "Europe/Dublin", "+353-1-234-5678", "work", "1 O'Connell St", "Dublin", null, "D01 F5P2", "IE"),
        new("mia.harris", "mia.harris@example.com", "Mia", "Harris", null, "Ms.", "Employee", "nl-NL", "nl_NL", "Europe/Amsterdam", "+31-20-123-4567", "mobile", "Damrak 1", "Amsterdam", null, "1012 LG", "NL"),
        new("noah.martin", "noah.martin@example.com", "Noah", "Martin", "William", "Mr.", "Contractor", "en-US", "en_US", "America/Denver", "+1-555-0114", "work", "1600 Pennsylvania Ave", "Denver", "CO", "80202", "US"),
        new("olivia.garcia", "olivia.garcia@example.com", "Olivia", "Garcia", null, "Ms.", "Employee", "pt-BR", "pt_BR", "America/Sao_Paulo", "+55-11-9876-5432", "mobile", "Av. Paulista 1000", "São Paulo", "SP", "01310-100", "BR"),
        new("peter.rodriguez", "peter.rodriguez@example.com", "Peter", "Rodriguez", "Miguel", "Mr.", "Manager", "es-MX", "es_MX", "America/Mexico_City", "+52-55-1234-5678", "work", "Paseo de la Reforma 1", "Mexico City", "CDMX", "06600", "MX"),
        new("quinn.lewis", "quinn.lewis@example.com", "Quinn", "Lewis", null, null, "Employee", "en-US", "en_US", "America/New_York", "+1-555-0117", "mobile", "42 Wallaby Way", "New York", "NY", "10001", "US"),
        new("rachel.lee", "rachel.lee@example.com", "Rachel", "Lee", "Anne", "Ms.", "Employee", "ko-KR", "ko_KR", "Asia/Seoul", "+82-2-1234-5678", "work", "1 Sejong-daero", "Seoul", null, "04524", "KR"),
        new("samuel.walker", "samuel.walker@example.com", "Samuel", "Walker", "David", "Mr.", "Contractor", "en-US", "en_US", "America/Los_Angeles", "+1-555-0119", "work", "1 Infinite Loop", "Cupertino", "CA", "95014", "US"),
        new("tina.hall", "tina.hall@example.com", "Tina", "Hall", null, "Ms.", "Manager", "en-US", "en_US", "America/Chicago", "+1-555-0120", "work", "233 S Wacker Dr", "Chicago", "IL", "60606", "US")
    ];

    internal static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var seed in Users)
        {
            if (await userManager.FindByNameAsync(seed.UserName) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = seed.UserName,
                Email = seed.Email,
                EmailConfirmed = true,
                PhoneNumber = seed.PhoneNumber,
                PhoneNumberConfirmed = true
            };

            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create seed user '{seed.UserName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            var claims = BuildClaims(seed);
            var claimsResult = await userManager.AddClaimsAsync(user, claims);
            if (!claimsResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to add claims for '{seed.UserName}': {string.Join(", ", claimsResult.Errors.Select(e => e.Description))}");
            }
        }
    }

    private static List<Claim> BuildClaims(SeedUser seed)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.GivenName, seed.GivenName),
            new(AppClaimTypes.FamilyName, seed.FamilyName),
            new(AppClaimTypes.DisplayName, $"{seed.GivenName} {seed.FamilyName}"),
            new(AppClaimTypes.UserType, seed.UserType),
            new(AppClaimTypes.PreferredLanguage, seed.PreferredLanguage),
            new(AppClaimTypes.Locale, seed.Locale),
            new(AppClaimTypes.Timezone, seed.Timezone),
            new(AppClaimTypes.PhoneNumber, seed.PhoneNumber),
            new(AppClaimTypes.PhoneNumberType, seed.PhoneNumberType),
            new(AppClaimTypes.StreetAddress, seed.StreetAddress),
            new(AppClaimTypes.Locality, seed.Locality),
            new(AppClaimTypes.PostalCode, seed.PostalCode),
            new(AppClaimTypes.Country, seed.Country)
        };

        if (seed.MiddleName is not null)
        {
            claims.Add(new Claim(AppClaimTypes.MiddleName, seed.MiddleName));
        }

        if (seed.Title is not null)
        {
            claims.Add(new Claim(AppClaimTypes.Title, seed.Title));
        }

        if (seed.Region is not null)
        {
            claims.Add(new Claim(AppClaimTypes.Region, seed.Region));
        }

        return claims;
    }

    private sealed record SeedUser(
        string UserName,
        string Email,
        string GivenName,
        string FamilyName,
        string? MiddleName,
        string? Title,
        string UserType,
        string PreferredLanguage,
        string Locale,
        string Timezone,
        string PhoneNumber,
        string PhoneNumberType,
        string StreetAddress,
        string Locality,
        string? Region,
        string PostalCode,
        string Country);
}
