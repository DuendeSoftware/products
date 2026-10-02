# IdentityServer Changelog

# 8.1.0

## Enhancements
- Added `AddStorage()` extension method
  - Registers the complete set of Duende.Storage-backed configuration and operational stores, admin services, schema services, and supporting infrastructure (DSOs, repositories, pooled storage factory, outbox subscription/processor, background purge hosted services) in a single call.
  - A concrete database provider (for example `AddSqlite()`, `AddPostgreSql()`, or `AddMsSql()`) is selected via the `configure` delegate passed to `AddStorage()`.
  - Explicit store selector methods (such as `AddInMemoryClients()`) called after `AddStorage()` replace the corresponding registration made by it.
- Cookies are now scoped to the resolved space path for path-based Spaces deployments, so two spaces can run independent login sessions in the same browser (#3428). There is no automatic migration of pre-existing root-scoped cookies; expect brief coexistence and possible reauthentication the first time a deployment enables path-based spaces. Configuring a `__Host-` cookie name on a non-root space path is now rejected; use the `__Secure-` prefix instead if you want a secure cookie name on a path-scoped space.

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

