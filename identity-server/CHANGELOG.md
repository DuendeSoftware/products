# IdentityServer Changelog

# 7.4.0-preview.1

## Breaking Changes
- Address CA1707 violations by @bhazen
  - This PR removed the unused Duende.IdentityServer.Models.DiscoveryDocument class which was public

## Enhancements
- Skip front-channel logout iframe when unnecessary by @bhazen
- Callback option for path detection in Dynamic Providers by @bhazen
- Improved UI locales support by @bhazen
  - Improves support for the `ui_locales` parameter in protocol request which support it to allow for better localization.
  - The default implementation, `DefaultUiLocalsService.cs`, delegates to the `CookieRequestCultureProvider` if it is present and any of the values passed in the
`ui_locales` parameter match a supported UI culture.
  - If the default implementation does not meet your needs, `IUiLocalesService` can be implemented and registered with DI.

## Bug Fixes
- Reject Pushed Authorization Requests with parameters duplicated in a JAR by @wcabus

## Code Quality
- Fixed typo in XML doc for Client.CoordinateLifetimeWithUserSession by @wcabus

