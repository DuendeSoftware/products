# SCIM.dev test host

This disposable host exposes the User Management SCIM implementation to
[SCIM Verify](https://scim.dev/verify/). It runs on local loopback, while VS Code
provides a public HTTPS tunnel that scim.dev can reach.

This host has also been used to run
[Microsoft's SCIM validator](https://scim.microsoft.com/) against the same
implementation, since it exercises interoperability against a second
independent SCIM client. Both validators share the same host configuration;
see "Known limitations" below for a validator-specific compatibility setting.

The host uses an in-memory SQLite database. Each restart creates a fresh database
with the default SCIM user schema and two sample users:

- `alice@example.com`
- `bob@example.com`

## Start the host

Open the repository root in VS Code:

```powershell
code .
```

In a VS Code terminal, choose a temporary bearer token and start the host:

```powershell
$env:ScimDev__BearerToken = 'choose-a-long-random-temporary-value'
dotnet run --project .\libs\user-management\hosts\ScimDev\ScimDev.csproj --launch-profile http
```

Keep this terminal running. The host listens on `http://127.0.0.1:6200`.

Check the local discovery endpoint:

```powershell
Invoke-RestMethod `
  -Uri 'http://127.0.0.1:6200/scim/ServiceProviderConfig' `
  -Headers @{ Accept = 'application/scim+json' }
```

Check the protected Users endpoint:

```powershell
Invoke-RestMethod `
  -Uri 'http://127.0.0.1:6200/scim/Users' `
  -Headers @{
      Accept = 'application/scim+json'
      Authorization = "Bearer $env:ScimDev__BearerToken"
  }
```

The Users response should contain the two sample users.

## Create a public VS Code tunnel

1. Run **Ports: Focus on Ports View** from the VS Code Command Palette.
2. Select **Forward a Port** and enter `6200`.
3. Right-click port 6200 and select **Port Visibility > Public**.
4. Copy the HTTPS **Forwarded Address**.

The host remains bound to loopback. VS Code terminates public HTTPS and forwards
requests to the local HTTP listener.

Test the public discovery endpoint from PowerShell:

```powershell
$tunnel = 'https://<forwarded-address>'

Invoke-RestMethod `
  -Uri "$tunnel/scim/ServiceProviderConfig" `
  -Headers @{ Accept = 'application/scim+json' }
```

Use an API `Accept` header for this check. A browser may show the Microsoft dev
tunnel anti-phishing interstitial on its first HTML request. SCIM API requests
normally bypass that page because they do not request HTML.

Confirm that `meta.location` in the response starts with the public HTTPS tunnel
address. This verifies that forwarded host and scheme information reaches the
application correctly.

## Configure SCIM Verify

Open [SCIM Verify](https://scim.dev/verify/) and enter:

| Setting | Value |
|---|---|
| SCIM Base URL | `https://<forwarded-address>/scim` |
| Authentication Scheme | `Bearer` |
| Bearer token | The value assigned to `ScimDev__BearerToken` |

Start with discovery or read-only checks before running provisioning checks.
Watch the host terminal to confirm that requests reach the application.

If SCIM Verify receives the dev tunnel interstitial, add this custom request
header in its advanced configuration if supported:

```http
X-Tunnel-Skip-AntiPhishing-Page: true
```

Otherwise, use a tunnel provider that does not place an interstitial in front of
API requests.

## Stop and clean up

1. Stop the host with `Ctrl+C`.
2. In the VS Code Ports view, right-click port 6200 and select
   **Stop Forwarding Port**.
3. Clear the token from the terminal:

   ```powershell
   Remove-Item Env:ScimDev__BearerToken
   ```

The forwarded port is public while active. Use only disposable data, do not
commit the token, and do not bind the host to `0.0.0.0`.

## Known limitations

`changePassword` is disabled in the SCIM schema advertised by this host.
Microsoft's SCIM validator cannot synthesize test data for the write-only
`password` attribute and throws instead of skipping it, so this host disables
schema advertisement of `changePassword` to keep it compatible with that
validator. This is a test-host-only setting; production hosts advertise
`changePassword` normally when password authentication is enabled. As a
result, this host cannot be used to test password-change interop with either
validator.
