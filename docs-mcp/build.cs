#:project ../.github/build/BuildHelpers.csproj

using BuildHelpers;
using static Bullseye.Targets;

var repoRoot = Repo.FindRoot();

Targets.Shared(repoRoot, "docs-mcp/docs-mcp.slnf");

Target(SharedTargets.Default, [
    SharedTargets.CheckSolutions,
    SharedTargets.CheckUnusedPackages,
    SharedTargets.CheckSortedRefs,
    SharedTargets.VerifyFormatting,
    SharedTargets.Clean,
    SharedTargets.VerifyNoChanges,
    SharedTargets.DotnetDevCerts
]);

await RunTargetsAndExitAsync(args);
