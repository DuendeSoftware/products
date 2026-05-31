// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Cryptography;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Profiles;

namespace Duende.Platform.UserManagement.Fixtures;

internal static class TestData
{
    internal const ulong UnixTimeSeconds2000 = 946684800UL;
    internal const ulong UnixTimeSeconds2005Minus60 = 1111111051UL;
    internal const ulong UnixTimeSeconds2005Minus30 = 1111111081UL;
    internal const ulong UnixTimeSeconds2005 = 1111111111UL;
    internal const ulong UnixTimeSeconds2005Plus30 = 1111111141UL;
    internal const ulong UnixTimeSeconds2005Plus60 = 1111111171UL;
    internal const ulong UnixTimeSeconds2009 = 1234567890UL;
    internal const ulong UnixTimeSeconds2033 = 2000000000UL;
    internal const ulong UnixTimeSeconds2603 = 20000000000UL;

    internal const string Totp2000 = "795445";
    internal const string Totp2005Minus60 = "731029";
    internal const string Totp2005Minus30 = "081804";
    internal const string Totp2005 = "050471";
    internal const string Totp2005Plus30 = "266759";
    internal const string Totp2005Plus60 = "306183";
    internal const string Totp2009 = "005924";
    internal const string Totp2033 = "279037";
    internal const string Totp2603 = "353130";

    private static int _counter;

    // Based on https://datatracker.ietf.org/doc/html/rfc6238#appendix-B
    // 12345678901234567890 in ASCII
    internal static readonly PlainBytesTotpKey TotpKey =
        PlainBytesTotpKey.DecodeFromBase32("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");

    internal static IEnumerable<Type> SubjectIdTypes =>
    [
        typeof(EmailAddress), typeof(OpaqueSubjectId), typeof(PhoneNumber)
    ];

    internal static OtpAddress CreateOtpAddress(Type subjectIdType)
    {
        if (subjectIdType == typeof(EmailAddress))
        {
            return new OtpAddress(OtpChannel.Email, EmailAddress.Create($"a{Count()}@b"));
        }

        if (subjectIdType == typeof(OpaqueSubjectId))
        {
            return new OtpAddress(OtpChannel.Create("App"), OpaqueSubjectId.Create($"{Count()}"));
        }

        if (subjectIdType == typeof(PhoneNumber))
        {
            return new OtpAddress(OtpChannel.Sms, PhoneNumber.Create($"+1 234 567 890 {Count()}"));
        }

        throw new ArgumentOutOfRangeException(nameof(subjectIdType));
    }

    internal static OtpAddress CreateOtpAddress() => CreateOtpAddress(SubjectIdTypes.First());

    internal static ExternalAuthenticator CreateExternalAuthenticator(Type subjectIdType)
    {
        var count = Count();
        var name = ExternalAuthenticatorName.Create($"{nameof(ExternalAuthenticator)}{count}");

        if (subjectIdType == typeof(EmailAddress))
        {
            return new ExternalAuthenticator(name, EmailAddress.Create($"a{count}@b"));
        }

        if (subjectIdType == typeof(OpaqueSubjectId))
        {
            return new ExternalAuthenticator(name, OpaqueSubjectId.Create($"{count}"));
        }

        if (subjectIdType == typeof(PhoneNumber))
        {
            return new ExternalAuthenticator(name, PhoneNumber.Create($"+1 234 567 890 {count}"));
        }

        throw new ArgumentOutOfRangeException(nameof(subjectIdType));
    }

    internal static ExternalAuthenticator CreateExternalAuthenticator() =>
        CreateExternalAuthenticator(SubjectIdTypes.First());

    internal static async Task<ValidatedPlainTextPassword> CreatePasswordAsync(IUserAuthenticatorsSelfService selfService, UserSubjectId? userId = null, Ct ct = default) =>
        await selfService.ValidatePasswordAsync(userId ?? UserSubjectId.New(), $"ABcd12!@{Count()}", ct);

    internal static async Task<(ValidatedPlainTextPassword Password, NonValidatedPassword Supplied)> CreatePasswordPairAsync(IUserAuthenticatorsSelfService selfService, UserSubjectId? userId = null, Ct ct = default)
    {
        var raw = $"ABcd12!@{Count()}";
        var password = await selfService.ValidatePasswordAsync(userId ?? UserSubjectId.New(), raw, ct);
        return (password, NonValidatedPassword.Create(raw));
    }

    internal static AttributeValueCollection CreateAttributes(IReadOnlyAttributeSchema schema)
    {
        var attributes = new AttributeValueCollection(schema);

        foreach (var definition in schema.AttributeDefinitions.Values)
        {
            var attribute = CreateAttribute(definition);
            if (attribute != null)
            {
                attributes.Set(attribute);
            }
        }

        return attributes;
    }

    private static AttributeValue? CreateAttribute(AttributeDefinition definition) =>
        definition.AttributeType switch
        {
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.
            ScalarAttributeType scalar => scalar.DataType switch
            {
                ScalarDataType.Boolean =>
                    AttributeValue.Load(definition.Code, Count() % 2 == 0),
                ScalarDataType.Date =>
                    AttributeValue.Load(definition.Code, DateOnly.FromDayNumber(Count())),
                ScalarDataType.DateTime =>
                    AttributeValue.Load(definition.Code, DateTimeOffset.FromUnixTimeSeconds(Count())),
                ScalarDataType.Decimal =>
                    AttributeValue.Load(definition.Code, (decimal)Count()),
                ScalarDataType.Integer =>
                    AttributeValue.Load(definition.Code, Count()),
                ScalarDataType.String =>
                    AttributeValue.Load(definition.Code, Count().ToString(CultureInfo.InvariantCulture))
            },
#pragma warning restore CS8524
            ComplexAttributeType complexType =>
                AttributeValue.Load<IReadOnlyDictionary<string, object>>(definition.Code,
                    complexType.Properties
                        .ToDictionary(p => p.Key.Value, _ => (object)Count().ToString(CultureInfo.InvariantCulture))),
            ListAttributeType listType =>
                AttributeValue.Load<IReadOnlyList<object>>(definition.Code,
                    (IReadOnlyList<object>)[CreateElementValue(listType.ElementType)]),
            _ => null
        };

    private static object CreateElementValue(AttributeType elementType) =>
        elementType switch
        {
#pragma warning disable CS8524
            ScalarAttributeType scalar => scalar.DataType switch
            {
                ScalarDataType.Boolean => (object)(Count() % 2 == 0),
                ScalarDataType.Date => DateOnly.FromDayNumber(Count()),
                ScalarDataType.DateTime => DateTimeOffset.FromUnixTimeSeconds(Count()),
                ScalarDataType.Decimal => (decimal)Count(),
                ScalarDataType.Integer => Count(),
                ScalarDataType.String => Count().ToString(CultureInfo.InvariantCulture)
            },
#pragma warning restore CS8524
            ComplexAttributeType complexType =>
                (IReadOnlyDictionary<string, object>)complexType.Properties
                    .ToDictionary(p => p.Key, _ => (object)Count().ToString(CultureInfo.InvariantCulture)),
            _ => Count().ToString(CultureInfo.InvariantCulture)
        };

    internal static async Task AddAttributeDefinitions(IUserProfileSchemaAdmin admin, Ct ct)
    {
        foreach (var definition in CreateAttributeDefinitions())
        {
            (await admin.TryAddAttributeDefinitionAsync(definition, ct)).ShouldBeTrue();
        }
    }

    internal static IEnumerable<AttributeDefinition> CreateAttributeDefinitions() =>
    [
        new() { Code = AttributeCode.Create("attribute_1"), AttributeType = new ScalarAttributeType(ScalarDataType.Boolean), Description = AttributeDescription.Create("description 1"), IsUnique = true },
        new() { Code = AttributeCode.Create("attribute_2"), AttributeType = new ScalarAttributeType(ScalarDataType.Date), Description = AttributeDescription.Create("description 2"), IsUnique = true },
        new() { Code = AttributeCode.Create("attribute_3"), AttributeType = new ScalarAttributeType(ScalarDataType.DateTime), Description = AttributeDescription.Create("description 3"), IsUnique = true },
        new() { Code = AttributeCode.Create("attribute_4"), AttributeType = new ScalarAttributeType(ScalarDataType.Decimal), Description = AttributeDescription.Create("description 4"), IsUnique = true },
        new() { Code = AttributeCode.Create("attribute_5"), AttributeType = new ScalarAttributeType(ScalarDataType.Integer), Description = AttributeDescription.Create("description 5"), IsUnique = true },
        new() { Code = AttributeCode.Create("attribute_6"), AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("description 6"), IsUnique = true }
    ];

    internal static IEnumerable<AttributeDefinition> CreateNonScalarAttributeDefinitions() =>
    [
        new AttributeDefinition
        {
            Code = AttributeCode.Create("attribute_9"),
            AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("first_name")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("last_name")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }),
            Description = AttributeDescription.Create("description 9")
        },
        new AttributeDefinition
        {
            Code = AttributeCode.Create("attribute_10"),
            AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
            Description = AttributeDescription.Create("description 10")
        },
        new AttributeDefinition
        {
            Code = AttributeCode.Create("attribute_11"),
            AttributeType = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("tag")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            })),
            Description = AttributeDescription.Create("description 11")
        }
    ];

    internal static async Task<UserSubjectId> CreateUserWithTotpAuthenticator(
        this IUserAuthenticatorsSelfService selfService,
        ulong unixTimeSeconds,
        PlainTextTotp totp,
        FakeTimeProvider timeProvider,
        Ct ct)
    {
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), CreateExternalAuthenticator(), ct: ct)).ShouldNotBeNull();
        timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)unixTimeSeconds));
        (await selfService.TryAddTotpAuthenticatorAsync(user.SubjectId, TotpAuthenticatorName.Default, TotpKey, totp, ct))
            .ShouldBeTrue();

        return user.SubjectId;
    }

    internal static PasskeyCredentialData CreatePasskeyCredential(string name) =>
        new(
            CredentialId: PasskeyCredentialId.From(Guid.NewGuid().ToByteArray()),
            PublicKeyCose: RandomNumberGenerator.GetBytes(32),
            Algorithm: CoseAlgorithms.Es256,
            SignCount: 0,
            BackupEligible: false,
            BackedUp: false,
            Aaguid: Guid.Empty,
            CreatedAt: DateTimeOffset.UtcNow,
            Name: name);

    private static int Count() => Interlocked.Increment(ref _counter);
}
