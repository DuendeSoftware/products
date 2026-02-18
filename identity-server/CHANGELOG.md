# IdentityServer Changelog

# 8.0.0

## Breaking Changes
- HTTP 303 (See Other) is now the unconditional redirect status code for all authorization and end-session redirects. The `UserInteractionOptions.UseHttp303Redirects` opt-in flag has been removed. This aligns IdentityServer with the FAPI 2.0 Security Profile (Section 5.3.2.2, item 11).

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

