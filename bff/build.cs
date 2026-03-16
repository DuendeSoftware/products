#:project ../.github/build/BuildHelpers.csproj

using BuildHelpers;
using static Bullseye.Targets;

var repoRoot = Repo.FindRoot();

Targets.Shared(repoRoot, "bff/bff.slnf");

const string TestsBffTests = "tests-bff-tests";
const string TestsHostsTests = "tests-hosts-tests";

Targets.Test(TestsBffTests, "bff/test/Bff.Tests", repoRoot);
Targets.Test(TestsHostsTests, "bff/test/Hosts.Tests", repoRoot);

Target(SharedTargets.Default, [
    SharedTargets.CheckSolutions,
    SharedTargets.CheckUnusedPackages,
    SharedTargets.CheckSortedRefs,
    SharedTargets.VerifyFormatting,
    SharedTargets.Clean,
    SharedTargets.VerifyNoChanges,
    SharedTargets.DotnetDevCerts,
    TestsBffTests,
    TestsHostsTests
]);

await RunTargetsAndExitAsync(args);
