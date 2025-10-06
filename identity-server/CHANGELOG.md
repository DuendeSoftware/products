# IdentityServer Changelog

# 7.4.0-preview.1

## Breaking Changes
- Address CA1707 violations by @bhazen
  - This PR removed the unused Duende.IdentityServer.Models.DiscoveryDocument class which was public
- Address CA2211 violations by @bhazen
  - This PR marked static properties referring to counters in Telemetry.cs as readonly

## Enhancements
- Add configuration profile abstraction by @josephdecock
  - Adds infrastructure to support configuration profiles that allow developers to express the intention that they are following a particular specification or profile, such as FAPI 2.0.
  - Adds `ConfigurationProfiles` static class with constants for well-known profiles (currently includes `Fapi2`).
  - Adds `ConfigurationProfileOptions` to `IdentityServerOptions` to enable profiles globally.
  - Adds `ConfigurationProfiles` property to `Client` model to enable profiles per-client.
  - This is foundational infrastructure; profile enforcement logic will be added in subsequent releases.
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
- Emit Telemetry Event for Introspection Requests for Valid Tokens by @bhazen

## Code Quality
- Fixed typo in XML doc for Client.CoordinateLifetimeWithUserSession by @wcabus

