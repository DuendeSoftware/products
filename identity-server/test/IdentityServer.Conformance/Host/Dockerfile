# Dockerfile for the Duende IdentityServer conformance test host.
# Serves all conformance plans: OidcCore, Logout, and FAPI 2.0.
#
# This image is intentionally runtime-only. The IdentityServer host is
# published on the build machine (workflow or test fixture) so the publish
# step has access to the Duende GitHub Packages feed via the user-level
# NuGet.Config. The published output is then copied into a slim runtime image.
#
# Build context is the repository root. The publish output must exist at:
#   libs/identity-server/test/IdentityServer.Conformance/Host/publish
# before `docker compose build` is invoked.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    || (sleep 2 && apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*)

COPY libs/identity-server/test/IdentityServer.Conformance/Host/publish/ ./

ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5001

ENTRYPOINT ["dotnet", "Host.IdentityServer.Conformance.dll"]
