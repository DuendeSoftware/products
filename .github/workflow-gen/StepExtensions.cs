// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;

public static class StepExtensions
{
    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNET_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
    {
        job.Step()
            .Name("List .net sdks")
            .Run("dotnet --list-sdks");

        job.Step()
            .Name("Setup .NET")
            .ActionsSetupDotNet("3e891b0cb619bf60e2c25674b222b8940e2c1c25", ["8.0.x", "9.0.103"]);
        // v4.1.0
    }

    /// <summary>
    /// Only run this for a main build
    /// </summary>
    public static Step IfRefMain(this Step step)
        => step.If("github.ref == 'refs/heads/main'");

    /// <summary>
    /// Only run this if the build is triggered on a branch IN the same repo
    /// this means it's from a trusted contributor.
    /// </summary>
    public static Step IfGithubEventIsPush(this Step step)
        => step.If("github.event == 'push'");

    public static void StepDotNetDevCerts(this Job job)
        => job.Step()
            .Name("Dotnet devcerts")
            .Run("dotnet dev-certs https --trust");

    public static void StepInstallPlayWright(this Job job)
        => job.Step()
            .Name("Install Playwright")
            .Run("pwsh test/Hosts.Tests/bin/Release/net9.0/playwright.ps1 install --with-deps");

    public static void StepToolRestore(this Job job)
        => job.Step()
            .Name("Tool restore")
            .Run("dotnet tool restore");

    public static void StepPack(this Job job, string target) =>
        job.Step()
            .Name($"Pack {target}")
            .Run($"dotnet pack -c Release {target} -o artifacts");

    public static Step StepRestore(this Job job, string solution)
        => job.Step()
            .Name("Restore")
            .Run($"dotnet restore {solution}");

    public static Step StepVerifyFormatting(this Job job, string solution)
        => job.Step()
            .Name("Verify Formatting")
            .Run($"dotnet format {solution} --verify-no-changes --no-restore");

    public static Step StepBuild(this Job job, string solution)
        => job.Step()
            .Name("Build")
            .Run($"dotnet build {solution} --no-restore -c Release");

    public static void StepTest(this Job job, string project)
    {
        var logFileName = $"{project}-tests.trx";
        var loggingFlags = $"--logger \"console;verbosity=normal\" " +
                           $"--logger \"trx;LogFileName={logFileName}\" " +
                           $"--collect:\"XPlat Code Coverage\"";

        job.Step()
            .Name($"Test - {project}")
            .Run($"dotnet test {project} -c Release --no-build {loggingFlags}");

        var id = $"test-report-{project.Replace("/", "-").Replace(".", "-")}";
        job.Step(id)
            .Name($"Test report - {project}")
            .WorkingDirectory("test")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If("github.event_name == 'push' && (success() || failure())")
            .With(
                ("name", $"Test Report - {project}"),
                ("path", $"**/{logFileName}"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));

        job.Step()
            .Name("Publish test report link")
            .Run($"echo \"[Test Results - {project}](${{{{ steps.{id}.outputs.url_html }}}})\" >> $GITHUB_STEP_SUMMARY");
    }

    public static Step StepPushToNuget(this Job job, bool pushAlways = false)
        => job.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY", pushAlways);

    public static Step StepPushToGithub(this Job job, GitHubContexts contexts, bool pushAlways = false)
        => job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN", pushAlways)
            .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
                ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));


    public static void StepSign(this Job job, bool always = false)
    {
        var flags = "--file-digest sha256 " +
                    "--timestamp-rfc3161 http://timestamp.digicert.com " +
                    "--azure-key-vault-url https://duendecodesigninghsm.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 " +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 " +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} " +
                    "--azure-key-vault-certificate NuGetPackageSigning";
        var step = job.Step()
            .Name("Sign packages");
        if (!always)
        {
            step = step.IfGithubEventIsPush();
        }

        step.Run($"""
                  for file in artifacts/*.nupkg; do
                     dotnet NuGetKeyVaultSignTool sign "$file" {flags}
                  done
                  """);
    }

    public static Step StepPush(this Job job, string destination, string sourceUrl, string secretName, bool pushAlways = false)
    {
        var apiKey = $"${{{{ secrets.{secretName} }}}}";
        var step = job.Step()
            .Name($"Push packages to {destination}");

        if (!pushAlways)
        {
            step.IfRefMain();
        }

        return step.Run($"dotnet nuget push artifacts/*.nupkg --source {sourceUrl} --api-key {apiKey} --skip-duplicate");
    }

    public static Step StepGitCheckoutCustomBranch(this Job job) =>
        job.Step()
            .Name("Checkout target branch")
            .If("github.event.inputs.branch != 'main'")
            .Run("git checkout ${{ github.event.inputs.branch }}");

    public static Step StepGitConfig(this Job job) =>
        job.Step()
            .Name("Git Config")
            .Run("""
                 git config --global user.email "github-bot@duendesoftware.com"
                 git config --global user.name "Duende Software GitHub Bot"
                 """);

    internal static Step StepGitRemoveExistingTagIfConfigured(this Job job, Product component, GitHubContexts contexts) =>
        job.Step()
            .Name("Git Config")
            .If("github.event.inputs['remove-tag-if-exists'] == 'true'")
            .Run($"""
                  if git rev-parse {component.TagPrefix}-{contexts.Event.Input.Version} >/dev/null 2>&1; then
                    git tag -d {component.TagPrefix}-{contexts.Event.Input.Version}
                    git push --delete origin {component.TagPrefix}-{contexts.Event.Input.Version}
                  else
                    echo 'Tag {component.TagPrefix}-{contexts.Event.Input.Version} does not exist.'
                  fi
                  """);

    internal static Step StepGitPushTag(this Job job, Product component, GitHubContexts contexts) =>
        job.Step()
            .Name("Git Config")
            .Run($"""
                  git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m "Release v{contexts.Event.Input.Version}"
                  git push origin {component.TagPrefix}-{contexts.Event.Input.Version}
                  """);

    public static WorkflowDispatch InputVersionBranchAndTagOverride(this WorkflowDispatch workflow) =>
        workflow.Inputs(
            new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"),
            new StringInput("branch", "(Optional) the name of the branch to release from", false, "main"),
            new BooleanInput("remove-tag-if-exists", "If set, will remove the existing tag. Use this if you have issues with the previous release action", false, false));

    public static Step StepUploadPlaywrightTestTraces(this Job job, string componentName)
    {
        var path = $"{componentName}/test/**/playwright-traces/*.zip";
        return job.Step()
            .Name("Upload playwright traces")
            .If("success() || failure()")
            .Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
            .With(
                ("name", "playwright-traces"),
                ("path", path),
                ("overwrite", "true"),
                ("retention-days", "15"));
    }

    public static void StepUploadArtifacts(this Job job, string componentName, bool uploadAlways = false)
    {
        var path = $"{componentName}/artifacts/*.nupkg";
        var step = job.Step()
            .Name("Upload Artifacts");

        if (!uploadAlways)
        {
            step.IfRefMain();
        }

        step.Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
            .With(
                ("name", "artifacts"),
                ("path", path),
                ("overwrite", "true"),
                ("retention-days", "15"));
    }

    /// <summary>
    /// The build triggers both on branch AND on pull_request.
    ///
    /// Only (trusted) contributors can open branches in the main repo, so these builds can run with a higher trust level.
    /// So, they are running with trigger 'push'. These builds have access to the secrets and thus they can do things like
    /// sign, push the packages, etc..
    /// 
    /// External contributors can only create branches on external repo's. These builds run with a lower trust level.
    /// So, they are running with trigger 'pull_request'. These builds do not have access to the secrets and thus they can't
    /// sign, push the packages, etc..
    ///
    /// Now, if a trusted contributor creates a branch in the main repo, then creates a PR, we don't want to run the build twice.
    /// This prevents that. The build will only run once, on the branch with the higher trust level.
    /// 
    /// </summary>
    public static Job RunEitherOnBranchOrAsPR(this Job job)
        => job.If(
            "(github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name != github.repository) || (github.event_name == 'push') || (github.event_name == 'workflow_dispatch')");

    public static void StepInitializeCodeQl(this Job job) =>
        job.Step()
            .Name("Initialize CodeQL")
            .Uses("github/codeql-action/init@9e8d0789d4a0fa9ceb6b1738f7e269594bdd67f0") // 3.28.9
            .With(
                ("languages", "csharp"),
                ("build-mode", "manual"),
                ("db-location", "~/.codeql/databases"));

    public static void StepPerformCodeQlAnalysis(this Job job) =>
        job.Step()
            .Name("Perform CodeQL Analysis")
            .Uses("github/codeql-action/analyze@9e8d0789d4a0fa9ceb6b1738f7e269594bdd67f0") // 3.28.9
            .With(
                ("category", "/language:csharp"));
}
