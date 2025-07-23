# IdentityServer Changelog

# 7.4.0-preview.1

## Breaking Changes
- Address CA1707 violations by @bhazen
  - This PR removed the unused Duende.IdentityServer.Models.DiscoveryDocument class which was public

## Enhancements
- Skip front-channel logout iframe when unnecessary by @bhazen
- Callback option for path detection in Dynamic Providers by @bhazen

## Bug Fixes
- Reject Pushed Authorization Requests with parameters duplicated in a JAR by @wcabus

## Code Quality
- Fixed typo in XML doc for Client.CoordinateLifetimeWithUserSession by @wcabus

