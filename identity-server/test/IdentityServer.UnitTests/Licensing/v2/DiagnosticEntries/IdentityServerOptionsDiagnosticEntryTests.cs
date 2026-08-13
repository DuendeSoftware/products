// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Microsoft.Extensions.Options;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class IdentityServerOptionsDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldExcludeLicenseKey()
    {
        var options = new IdentityServerOptions
        {
            LicenseKey = "test-key"
        };
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("IdentityServerOptions").TryGetProperty("LicenseKey", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_ShouldExcludePathMatchingCallback()
    {
        var options = new IdentityServerOptions();
        options.DynamicProviders.PathMatchingCallback = _ => Task.FromResult((string)null!);
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var dynamicProviders = result.RootElement.GetProperty("IdentityServerOptions").GetProperty("DynamicProviders");
        dynamicProviders.TryGetProperty("PathMatchingCallback", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_ShouldIncludeOtherProperties()
    {
        var options = new IdentityServerOptions
        {
            IssuerUri = "https://example.com",
            LowerCaseIssuerUri = true,
            AccessTokenJwtType = "jwt",
            LogoutTokenJwtType = "logout",
            EmitStaticAudienceClaim = true,
            EmitScopesAsSpaceDelimitedStringInJwt = true,
            EmitIssuerIdentificationResponseParameter = false,
            EmitStateHash = true,
            StrictJarValidation = true,
            ValidateTenantOnAuthorization = true,
            JwtValidationClockSkew = TimeSpan.FromMinutes(1),
            SupportedClientAssertionSigningAlgorithms = ["RS256", "ES256"],
            SupportedRequestObjectSigningAlgorithms = ["SHA256", "SHA512"],
            Diagnostics = new DiagnosticOptions { LogFrequency = TimeSpan.FromMinutes(30) }
        };
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var identityServerOptions = result.RootElement.GetProperty("IdentityServerOptions");
        identityServerOptions.GetProperty("IssuerUri").GetString().ShouldBe("https://example.com");
        identityServerOptions.GetProperty("LowerCaseIssuerUri").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("AccessTokenJwtType").GetString().ShouldBe("jwt");
        identityServerOptions.GetProperty("LogoutTokenJwtType").GetString().ShouldBe("logout");
        identityServerOptions.GetProperty("EmitStaticAudienceClaim").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("EmitScopesAsSpaceDelimitedStringInJwt").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("EmitIssuerIdentificationResponseParameter").GetBoolean().ShouldBeFalse();
        identityServerOptions.GetProperty("EmitStateHash").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("StrictJarValidation").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("ValidateTenantOnAuthorization").GetBoolean().ShouldBeTrue();
        identityServerOptions.TryGetProperty("Endpoints", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Discovery", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Authentication", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Events", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("InputLengthRestrictions", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("UserInteraction", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Caching", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Cors", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Csp", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Validation", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("DeviceFlow", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Ciba", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Logging", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("MutualTls", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("KeyManagement", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("PersistentGrants", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("DPoP", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Diagnostics", out _).ShouldBeTrue();

        identityServerOptions.GetProperty("JwtValidationClockSkew").GetString().ShouldBe(TimeSpan.FromMinutes(1).ToString());
        var supportedClientAssertionSigningAlgorithms = identityServerOptions.TryGetStringArray("SupportedClientAssertionSigningAlgorithms");
        supportedClientAssertionSigningAlgorithms.ShouldBe(["RS256", "ES256"]);
        var supportedRequestObjectSigningAlgorithms = identityServerOptions.TryGetStringArray("SupportedRequestObjectSigningAlgorithms");
        supportedRequestObjectSigningAlgorithms.ShouldBe(["SHA256", "SHA512"]);
        identityServerOptions.TryGetProperty("Preview", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_ShouldSucceedWhenAllDelegatePropertiesAreSet()
    {
        var options = new IdentityServerOptions();
        SetAllDelegateProperties(options);
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        // If a new delegate property is added to IdentityServerOptions (or any nested
        // options type) without also excluding it from diagnostic serialization, this
        // call will throw NotSupportedException.
        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.TryGetProperty("IdentityServerOptions", out _).ShouldBeTrue();
    }

    /// <summary>
    /// Walks the object graph rooted at <paramref name="root"/> and assigns a no-op
    /// delegate to every public read/write property whose type derives from
    /// <see cref="Delegate"/>. This ensures the serialization guard test covers any
    /// delegate properties added in the future.
    /// </summary>
    private static void SetAllDelegateProperties(object root)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        SetAllDelegatePropertiesRecursive(root, visited);
    }

    private static void SetAllDelegatePropertiesRecursive(object obj, HashSet<object> visited)
    {
        if (!visited.Add(obj))
        {
            return;
        }

        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (!prop.CanRead)
            {
                continue;
            }

            if (typeof(Delegate).IsAssignableFrom(prop.PropertyType) && prop.CanWrite)
            {
                prop.SetValue(obj, CreateNoOpDelegate(prop.PropertyType));
                continue;
            }

            if (prop.PropertyType.IsClass
                && prop.PropertyType != typeof(string)
                && !prop.PropertyType.IsArray
                && prop.GetIndexParameters().Length == 0)
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    SetAllDelegatePropertiesRecursive(value, visited);
                }
            }
        }
    }

    private static Delegate CreateNoOpDelegate(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke")!;
        var returnType = invoke.ReturnType;

        // Build a lambda that returns default for the delegate's return type
        var parameters = invoke.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();

        Expression body;
        if (returnType == typeof(void))
        {
            body = Expression.Empty();
        }
        else
        {
            body = Expression.Default(returnType);
        }

        var lambda = Expression.Lambda(delegateType, body, parameters);
        return lambda.Compile();
    }
}
