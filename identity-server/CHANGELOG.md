# IdentityServer Changelog

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

## Bug Fixes
- Reject Pushed Authorization Requests with parameters duplicated in a JAR by @wcabus

## Code Quality
- Fixed typo in XML doc for Client.CoordinateLifetimeWithUserSession by @wcabus

