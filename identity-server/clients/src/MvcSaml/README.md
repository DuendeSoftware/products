# MvcSaml

This client demonstrates SAML 2.0 single sign-on and single logout against IdentityServer.

## SP Certificate

The SP certificate is required for two SAML best practices:

- **Signed AuthnRequests** — the SP signs every authentication request it sends to the IdP, proving the request originated from this SP and has not been tampered with.
- **SP-initiated Single Logout (SLO)** — the SP signs logout requests sent to the IdP. The IdP always requires signed logout requests.

Without the certificate, the SSO login flow still works, but AuthnRequest signing is disabled and SP-initiated single logout will fail.

### Generating the certificate

Create a self-signed certificate with `openssl`:

```sh
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 3650 -nodes -subj "/CN=MvcSaml SP"
openssl pkcs12 -export -out saml-sp.pfx -inkey key.pem -in cert.pem -passout pass:changeit
rm key.pem cert.pem
```

Place `saml-sp.pfx` in this project directory (`clients/src/MvcSaml/`). The file is excluded from source control.

After generating the certificate, **restart both this app and the IdentityServer host** so both sides pick up the new public key.

### Why both sides need to restart

The MvcSaml SP reads the certificate at startup to configure request signing. The IdentityServer host also reads the same certificate file at startup to register the SP's public key for signature validation. Both must be restarted whenever the certificate is regenerated.

## Without the certificate

| Feature | Without certificate | With certificate |
|---|---|---|
| SSO (login) | Works | Works |
| AuthnRequest signing | Disabled | Enabled (always signed) |
| SP-initiated Single Logout | Fails (unsigned logout request rejected by IdP) | Works |
| IdP-initiated Single Logout | Works (IdP signs its own requests) | Works |
