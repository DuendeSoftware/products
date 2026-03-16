#:project ../.github/build/BuildHelpers.csproj

using BuildHelpers;
using static Bullseye.Targets;

var repoRoot = Repo.FindRoot();

Targets.Shared(repoRoot, "identity-server/identity-server.slnf");

const string TestsIdentityServerUnitTests = "tests-identity-server-unit-tests";
const string TestsIdentityServerIntegrationTests = "tests-identity-server-integration-tests";
const string TestsIdentityServerEndToEndTests = "tests-identity-server-end-to-end-tests";

Targets.Test(TestsIdentityServerUnitTests, "identity-server/test/IdentityServer.UnitTests", repoRoot);
Targets.Test(TestsIdentityServerIntegrationTests, "identity-server/test/IdentityServer.IntegrationTests", repoRoot);
Targets.Test(TestsIdentityServerEndToEndTests, "identity-server/test/IdentityServer.EndToEndTests", repoRoot);

Target(SharedTargets.Default, [
    SharedTargets.CheckSolutions,
    SharedTargets.CheckUnusedPackages,
    SharedTargets.CheckSortedRefs,
    SharedTargets.VerifyFormatting,
    SharedTargets.Clean,
    SharedTargets.VerifyNoChanges,
    SharedTargets.DotnetDevCerts,
    TestsIdentityServerUnitTests,
    TestsIdentityServerIntegrationTests,
    TestsIdentityServerEndToEndTests
]);

await RunTargetsAndExitAsync(args);
