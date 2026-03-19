#:project ../.github/build/BuildHelpers.csproj

using BuildHelpers;
using static Bullseye.Targets;

var repoRoot = Repo.FindRoot();

Targets.Shared(repoRoot, "aspnetcore-authentication-jwtbearer/aspnetcore-authentication-jwtbearer.slnf");

const string TestsAspNetCoreAuthenticationJwtBearerTests = "tests-asp-net-core-authentication-jwt-bearer-tests";

Targets.Test(TestsAspNetCoreAuthenticationJwtBearerTests, "aspnetcore-authentication-jwtbearer/test/AspNetCore.Authentication.JwtBearer.Tests", repoRoot);

Target(SharedTargets.Default, [
    SharedTargets.CheckSolutions,
    SharedTargets.CheckUnusedPackages,
    SharedTargets.CheckSortedRefs,
    SharedTargets.CheckSortedSlnf,
    SharedTargets.VerifyFormatting,
    SharedTargets.Clean,
    SharedTargets.VerifyNoChanges,
    SharedTargets.DotnetDevCerts,
    TestsAspNetCoreAuthenticationJwtBearerTests
]);

await RunTargetsAndExitAsync(args);
