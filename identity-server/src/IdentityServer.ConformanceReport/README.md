# Duende.IdentityServer.ConformanceReport

_OAuth 2.1 and FAPI 2.0 Security Profile conformance assessment for Duende IdentityServer._

## Overview

`Duende.IdentityServer.ConformanceReport` adds conformance assessment to your IdentityServer application. It evaluates your server and client configuration against OAuth 2.1 and FAPI 2.0 Security Profile requirements and generates an HTML report accessible via a protected endpoint.

## Installation

```bash
dotnet add package Duende.IdentityServer.ConformanceReport
```

## Setup

### 1. Add to your IdentityServer configuration

```csharp
builder.Services
    .AddIdentityServer()
    .AddInMemoryClients(clients)
    .AddConformanceReport(options =>
    {
        options.Enabled = true;
        options.EnableOAuth21Assessment = true;
        options.EnableFapi2SecurityAssessment = true;

        // Authorization is configured automatically - requires authenticated user by default
        // Customize as needed:
        options.ConfigureAuthorization = policy => policy
            .RequireRole("Administrator")
            .RequireClaim("department", "Compliance");
    });
```

### 2. Map the endpoint

```csharp
app.MapConformanceReport();
```

### 3. Access the report

Navigate to: `https://your-server/_duende/conformance-report`

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Enabled` | `bool` | `false` | Enable/disable conformance endpoints |
| `EnableOAuth21Assessment` | `bool` | `true` | Enable OAuth 2.1 profile assessment |
| `EnableFapi2SecurityAssessment` | `bool` | `true` | Enable FAPI 2.0 Security Profile assessment |
| `PathPrefix` | `string` | `"_duende"` | Path prefix for conformance endpoints (without leading slash) |
| `ConfigureAuthorization` | `Action<AuthorizationPolicyBuilder>?` | Requires authenticated user | Configure authorization policy for the HTML report endpoint |
| `AuthorizationPolicyName` | `string` | `"ConformanceReport"` | ASP.NET Core authorization policy name (used internally) |
| `HostCompanyName` | `string?` | `null` | Optional display name of the host company to include in the report |
| `HostCompanyLogoUrl` | `Uri?` | `null` | Optional URL of the host company's logo |

### Authorization Examples

**Default (requires authenticated user):**
```csharp
options.Enabled = true;
// ConfigureAuthorization defaults to requiring authenticated user
```

**Require specific role:**
```csharp
options.ConfigureAuthorization = policy => policy.RequireRole("Admin");
```

**Require multiple conditions:**
```csharp
options.ConfigureAuthorization = policy => policy
    .RequireRole("Admin")
    .RequireClaim("department", "IT");
```

**Allow anonymous (for development/testing only):**
```csharp
options.ConfigureAuthorization = policy =>
    policy.RequireAssertion(_ => builder.Environment.IsDevelopment());
```

**Manual policy registration (advanced scenarios):**
```csharp
// In AddConformanceReport:
options.ConfigureAuthorization = null; // Skip automatic registration

// Then manually register the policy:
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ConformanceReport", policy =>
    {
        policy.Requirements.Add(new MyCustomRequirement());
    });
});
```

> **Important**: If you set `ConfigureAuthorization = null`, you **must** manually register a policy with the name specified in `AuthorizationPolicyName` (default: `"ConformanceReport"`). Otherwise, the endpoint will fail at runtime with a "policy not found" error.

## Understanding the Report

The HTML report displays:
- **Server Configuration**: Matrix showing server-level conformance rules
- **Client Configurations**: Matrix showing per-client conformance rules
- **Rule Legend**: Explanation of each rule ID
- **Notes**: Detailed messages for warnings and failures

### Status Indicators

| Symbol | Meaning |
|--------|---------|
| Pass | Requirement met |
| Fail | Requirement not met (non-conformant) |
| Warning | Recommended practice not followed |
| N/A | Rule not applicable |

## Licensing

Duende IdentityServer is source-available, but requires a paid [license](https://duendesoftware.com/products/identityserver) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments.
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support

- For bug reports or feature requests, [use our developer community forum](https://duende.link/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.
