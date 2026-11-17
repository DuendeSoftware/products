# IdentityServer Changelog

# 8.1.0

## Breaking Changes
- **Dynamic SAML providers:** update your external login callback to copy the SAML sign-in claims, or sessions created through it stay unprotected against IdP-initiated logout from another configured IdP. See the #4608 entry under Bug Fixes for the code (#4608)
- `IServerSideSessionStore.UpdateSessionAsync` now returns `Task<bool>` and saves a session only while the version it was read at is still the stored version (#4342)
  - It returns `false` and saves nothing when the session was changed or removed since it was read. It never creates a removed session again.
  - A session with `ServerSideSession.Version` 0 was not read from the store and is saved without the check.
  - Custom implementations and decorators of `IServerSideSessionStore` must change `UpdateSessionAsync` to return the result and to do this check; a decorator must return the result of the store it wraps.
  - Code that calls `UpdateSessionAsync` should check the result: `false` means the save did not happen.

## Enhancements
- Added `AddStorage()` extension method
  - Registers the complete set of Duende.Storage-backed configuration and operational stores, admin services, schema services, and supporting infrastructure (DSOs, repositories, pooled storage factory, outbox subscription/processor, background purge hosted services) in a single call.
  - A concrete database provider (for example `AddSqlite()`, `AddPostgreSql()`, or `AddMsSql()`) is selected via the `configure` delegate passed to `AddStorage()`.
  - Explicit store selector methods (such as `AddInMemoryClients()`) called after `AddStorage()` replace the corresponding registration made by it.
- Cookies are now scoped to the resolved space path for path-based Spaces deployments, so two spaces can run independent login sessions in the same browser (#3428). There is no automatic migration of pre-existing root-scoped cookies; expect brief coexistence and possible reauthentication the first time a deployment enables path-based spaces. Configuring a `__Host-` cookie name on a non-root space path is now rejected; use the `__Secure-` prefix instead if you want a secure cookie name on a path-scoped space.

## Bug Fixes
- Concurrent updates to a server-side session no longer silently overwrite each other (#4342)
  - Previously a token call that extended the session, or a second browser request, could save a copy of the session it read earlier and undo a claim the browser had just added, an app just added to the client list, or the extension itself.
  - Each session now has a version, carried in `ServerSideSession.Version`. `IServerSideSessionStore.UpdateSessionAsync` saves a session only while the version it was read at is still the stored version (see Breaking Changes).
  - A browser request whose save is refused fails (HTTP 500), except a cookie renewal that only moves the session's dates forward and keeps its lifetime: that save is skipped, the request succeeds, and the next request renews again. A token call whose extension is refused still succeeds and skips that extension; the next token call extends the session again. Under frequent browser saves an extension can be skipped, so a session can end sooner than before.
  - A browser request also fails when its session is removed after the store looked it up and before the save. Previously the Entity Framework and Duende.Storage stores logged this, and the in-memory store created the session again. When the session is already gone at the lookup, the save is skipped, as before, and the session is not created again.
  - Entity Framework stores: expired session cleanup deletes only sessions that are still expired when it deletes them, and reports only those. The deletes of a batch run in one transaction, so a failure part way through deletes none of them and the next cleanup run picks them up again. Previously a session renewed while the cleanup ran was deleted, and the cleanup revoked its coordinated tokens and sent logout notifications for it.
  - Entity Framework stores: the `ServerSideSessions` table gets a `Version` column. Generate and apply a migration before you deploy. Without it, every session read and delete fails because the `Version` column is missing, and a new sign-in stores no server-side session.
  - During a rolling upgrade, saves made by nodes on an earlier version are not checked, so the protection starts when all nodes run this version.
- A SAML `LogoutRequest` from an upstream IdP ends the local session only when it names that session, for sessions that carry the SAML sign-in (#4608)
  - **Action required for dynamic SAML providers to be protected:** an external login callback that creates the session from the SAML handler's external sign-in (dynamic SAML providers, or a SAML scheme with a custom sign-in scheme) must copy three claims of the external user into the session, the same way it copies the `sid` claim of an OpenID Connect provider. Without them the session carries no SAML sign-in, and any validly signed `LogoutRequest` from any configured SAML IdP still ends it, as before. In a callback based on our templates, add this to `CaptureExternalLoginContext`, next to the `sid` copy:

    ```csharp
    localClaims.AddRange(externalResult.Principal.Claims.Where(claim => claim.Type
        is "https://duendesoftware.com/Saml2/Scheme"
        or "https://duendesoftware.com/Saml2/LogoutNameIdentifier"
        or "https://duendesoftware.com/Saml2/SessionIndex"));
    ```

    Copy the external user's claims themselves, as above, not new claims with the same values: the check compares the IdP that sent the request with the issuer of the `LogoutNameIdentifier` claim. Copying them also lets a sign-out send a `LogoutRequest` to the IdP for these sessions.
  - Previously IdentityServer, as a SAML service provider, checked only that the request was signed by the IdP in its `Issuer`, and then ended whatever session the browser had.
  - The SAML handler adds three claims to the user it signs in: `https://duendesoftware.com/Saml2/Scheme` (new), and, issued by the IdP's entity id, `https://duendesoftware.com/Saml2/LogoutNameIdentifier` and `https://duendesoftware.com/Saml2/SessionIndex`. It adds the last two only when the IdP's assertion carries a `SessionIndex`. `AddSamlServiceProvider()` with its default sign-in scheme signs the user straight into the session, so these sessions carry the claims without a change.
  - When the session's user carries the three claims, the request must come through the SAML scheme that signed the user in, from the same IdP, name the same NameID (and NameID format, when the request sends one) and, when it names a session index, name the session's one (SAML 2.0 Core, section 3.7.3.1). A request that names no session index ends the session of the named user. When a request lists several session indexes, only the first is compared, so a request whose first index is not the session's is refused. A refused request leaves the session and answers the IdP with a `LogoutResponse` with status `Requester`.
  - Sessions whose user does not carry the three claims are ended by the request as before: sessions created by a callback that does not copy them, sessions created before the upgrade, sign-ins without a `SessionIndex`, and sessions that did not come from SAML.
  - A request that arrives without a session no longer signs out, so it no longer deletes the browser's session cookie; the IdP gets a `LogoutResponse` with status `Success`. Before, any configured IdP could delete a `SameSite=Lax` or `Strict` session cookie this way, because the browser does not send such a cookie on a cross-site POST but does apply the response's cookie deletion. For the same reason, with a `SameSite=Lax` or `Strict` session cookie, a `LogoutRequest` that the IdP sends by a cross-site POST no longer ends the session; the default, `SameSite=None`, is not affected.
- A SAML message that an upstream IdP sends over the HTTP-Redirect binding is checked against the same parameter values that IdentityServer then uses (#4607)
  - Previously text added after a raw `=` in a signed `RelayState` was not covered by the signature check, and IdentityServer, as a SAML service provider, returned that changed `RelayState` to the IdP in its signed `LogoutResponse`. A `RelayState` that the IdP signed with a raw `=` in it is now accepted; before, its signature check failed.
  - A `LogoutRequest` over HTTP-Redirect that carries a `RelayState` is now rejected unless the query string signature (`SigAlg` and `Signature`) covers it. An XML signature inside the message does not cover the `RelayState`, and SAML 2.0 Bindings section 3.4.4.1 requires the IdP to remove it on this binding. An IdP that keeps the XML signature on HTTP-Redirect and sends a `RelayState` gets a failed logout after the upgrade.
  - A message with a duplicate `SAMLRequest`, `SAMLResponse`, `RelayState`, `SigAlg` or `Signature` parameter, or with `SigAlg` but no `Signature`, now fails with a SAML exception instead of a framework exception.
  - This covers the HTTP-Redirect binding. On HTTP-POST the IdP does not sign the `RelayState` (SAML 2.0 Bindings section 3.5.3), and this change does not affect it.

# 8.0.0

## Breaking Changes
- HTTP 303 (See Other) is now the unconditional redirect status code for all authorization and end-session redirects. The `UserInteractionOptions.UseHttp303Redirects` opt-in flag has been removed. This aligns IdentityServer with the FAPI 2.0 Security Profile (Section 5.3.2.2, item 11).

## Enhancements
- Tightened input length handling in access token and DPoP proof validation by @josephdecock
  - These changes are defense in depth. Both paths already rejected oversized input, and we are not aware of either being exploitable.
  - `TokenValidator` bounds the scan that tells a JWT apart from a reference token, so an over-long token is measured against `InputLengthRestrictions` without being read end to end first.
  - `InputLengthRestrictions.DPoPProofToken` is now applied when a DPoP proof is presented to a protected resource. Previously it was only applied at the token and pushed authorization endpoints. `DefaultDPoPProofValidator` also checks it, so the restriction holds for callers that do not check it themselves.
  - As a result, a valid DPoP proof longer than `InputLengthRestrictions.DPoPProofToken` is now rejected at protected resources, where it was previously accepted. The default limit is 4000 characters, so this is unlikely to affect you unless you emit unusually large proofs.
- Added `InputLengthRestrictions.Prompt`, applied to the `prompt` and `suppressed_prompt` parameters at the authorize endpoint by @josephdecock
  - The other free-form authorize parameters already had a length restriction, so this closes a gap rather than introducing a new kind of check. It is also defense in depth.
  - The default is 100 characters. A longer `prompt` is now rejected, where it was previously accepted. The longest combination of built-in prompt modes is `login consent select_account`, at 28 characters, so this is unlikely to affect you unless you configure long custom values in `UserInteractionOptions.PromptValuesSupported`.

# 7.4.0-preview.1

## Breaking Changes
- Address CA1707 violations by @bhazen
  - This PR removed the unused Duende.IdentityServer.Models.DiscoveryDocument class which was public
- Address CA2211 violations by @bhazen
  - This PR marked static properties referring to counters in Telemetry.cs as readonly

## Enhancements
- Skip front-channel logout iframe when unnecessary by @bhazen
- Callback option for path detection in Dynamic Providers by @bhazen
- Improved UI locales support by @bhazen
  - Improves support for the `ui_locales` parameter in protocol request which support it to allow for better localization.
  - The default implementation, `DefaultUiLocalsService.cs`, delegates to the `CookieRequestCultureProvider` if it is present and any of the values passed in the
`ui_locales` parameter match a supported UI culture.
  - If the default implementation does not meet your needs, `IUiLocalesService` can be implemented and registered with DI.
- Set the DisplayName of the activity associated with the incoming HttpRequest when IdentityServer routes are matched by @josephdecock
  This makes the IdentityServer route names appear in OTel traces.
- Support for custom parameters in the Authorize Redirect Uri by @bhazen
  - Adds a new `CustomParameters` property to `AuthorizeResponse` to support adding custom query parameters to the redirect uri. This will typically be used in conjunction with a custom `IAuthorizeResponseGenerator`.
- Updated ASP.NET Identity package to persist session claims based on an interface @bhazen
  - The ASP.NET Identity integration package now persists session claims based on `ISessionClaimsFilter.FilterToSessionClaimsAsync` which comes with a default implementation
  - The new interface can be implemented to customize which session claims are persisted in non-default scenarios.
## Bug Fixes
- Reject Pushed Authorization Requests with parameters duplicated in a JAR by @wcabus
- Emit Telemetry Event for Introspection Requests for Valid Tokens by @bhazen
- Consolidated EF Core versions to prevent missing method exceptions by @bhazen

## Code Quality
- Fixed typo in XML doc for Client.CoordinateLifetimeWithUserSession by @wcabus

