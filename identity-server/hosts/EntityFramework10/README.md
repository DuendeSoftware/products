# EntityFramework10 Host

## WS-Federation Dynamic Provider (Entra ID)

This host demonstrates a custom WS-Federation dynamic provider using Microsoft Entra ID.

### Entra App Registration Setup

1. In the Azure portal, go to **App registrations** and create a new registration.
2. Under **Authentication**, add a **Web** platform with redirect URI:
   ```
   https://localhost:5001/federation/dynamicprovider-entra-wsfed/signin
   ```
3. Under **Expose an API**, set the **Application ID URI** (e.g., `api://{client-id}`).
4. Note your **Tenant ID** and **Application (client) ID**.

### Configure User Secrets

The WS-Federation provider configuration is seeded into the database by the migrations project. Set the following user secrets on the **IdentityServerDb** migrations project:

```bash
cd identity-server/migrations/IdentityServerDb
dotnet user-secrets set "WsFed:MetadataAddress" "https://login.microsoftonline.com/{tenant-id}/federationmetadata/2007-06/federationmetadata.xml"
dotnet user-secrets set "WsFed:Wtrealm" "api://{client-id}"
```

Replace `{tenant-id}` and `{client-id}` with your Entra tenant and app registration values.

### Running

After configuring secrets, reset the database (e.g., nuke the SQL Server container) and restart via Aspire. The migrations project will seed the WS-Federation provider with your Entra configuration.
