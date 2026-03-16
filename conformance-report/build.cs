#:project ../.github/build/BuildHelpers.csproj

using BuildHelpers;
using static Bullseye.Targets;

var repoRoot = Repo.FindRoot();

Targets.Shared(repoRoot, "conformance-report/conformance-report.slnf");

const string TestsConformanceReportTests = "tests-conformance-report-tests";

Targets.Test(TestsConformanceReportTests, "conformance-report/test/ConformanceReport.Tests", repoRoot);

Target(SharedTargets.Default, [
    SharedTargets.CheckSolutions,
    SharedTargets.CheckUnusedPackages,
    SharedTargets.CheckSortedRefs,
    SharedTargets.VerifyFormatting,
    SharedTargets.Clean,
    SharedTargets.VerifyNoChanges,
    SharedTargets.DotnetDevCerts,
    TestsConformanceReportTests
]);

await RunTargetsAndExitAsync(args);
