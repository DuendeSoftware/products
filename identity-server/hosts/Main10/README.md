# Main10 Host

## WS-Federation Dynamic Provider (Entra ID)

This host demonstrates a custom WS-Federation dynamic provider using the in-memory store.

### Entra App Registration Setup

1. In the Azure portal, go to **App registrations** and create a new registration.
2. Under **Authentication**, add a **Web** platform with redirect URI:
   ```
   https://localhost:5001/federation/dynamicprovider-entra-wsfed/signin
   ```
3. Under **Expose an API**, set the **Application ID URI** (e.g., `api://{client-id}`).
4. Note your **Tenant ID** and **Application (client) ID**.

### Configure

Update the `WsFedProvider` in `IdentityServerExtensions.cs` with your Entra values:

- `MetadataAddress`: `https://login.microsoftonline.com/{tenant-id}/federationmetadata/2007-06/federationmetadata.xml`
- `Wtrealm`: `api://{client-id}`

Replace `{tenant-id}` and `{client-id}` with your Entra tenant and app registration values.
