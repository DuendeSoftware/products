// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;
using static GitHubContexts;

var contexts = Instance;

{
    SystemDescription identityServer = new("identity-server", "identity-server.slnf", "is");
    GenerateIdentityServerWorkflow(identityServer);
    GenerateIdentityServerReleaseWorkflow(identityServer);
}

{
    SystemDescription bff = new("bff", "bff.slnf", "bff");
    GenerateBffWorkflow(bff);
    GenerateBffReleaseWorkflow(bff);
}


void GenerateIdentityServerWorkflow(SystemDescription system)
{
    var workflow = new Workflow($"{system.Name}/ci");
    var paths = new[]
    {
        $".github/workflows/{system.Name}-**",
        $"{system.Name}/**",
        "Directory.Packages.props"
    };

    workflow.On
        .WorkflowDispatch();
    workflow.On
        .Push()
        .Paths(paths);
    workflow.On
        .PullRequest()
        .Paths(paths);

    workflow.EnvDefaults();

    var job = workflow
        .Job("build")
        .RunEitherOnBranchOrAsPR()
        .Name("Build")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", system.Name)
        .Job;

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    job.TimeoutMinutes(15);

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    job.StepBuild(system.Solution);

    job.StepTest(system.Solution);

    job.StepToolRestore();

    job.StepPackSolution(system.Solution);

    job.StepSign();

    job.StepPushToMyGet();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(system.Name);

    var fileName = $"{system.Name}-ci";
    WriteWorkflow(workflow, fileName);
}
void GenerateIdentityServerReleaseWorkflow(SystemDescription system)
{
    var workflow = new Workflow($"{system.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .Inputs(new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"));

    workflow.EnvDefaults();

    var job = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", system.Name).Job;

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    job.Step()
        .Name("Git tag")
        .Run($@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""
git tag -a {system.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {system.TagPrefix}-{contexts.Event.Input.Version}");

    job.StepPackSolution(system.Solution);

    job.StepToolRestore();

    job.StepSign();

    job.StepPushToMyGet();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(system.Name);

    var publishJob = workflow.Job("publish")
        .Name("Publish to nuget.org")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Needs("tag")
        .Environment("nuget.org", "");

    publishJob.Step()
        .Uses("actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16") // 4.1.8
        .With(("name", "artifacts"), ("path", "artifacts"));

    publishJob.StepSetupDotNet();

    publishJob.Step()
        .Name("List files")
        .Shell("bash")
        .Run("tree");

    publishJob.StepPushToNuget();

    var fileName = $"{system.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void GenerateBffWorkflow(SystemDescription system)
{
    var workflow = new Workflow($"{system.Name}/ci");
    var paths = new[]
    {
        $".github/workflows/{system.Name}-**",
        $"{system.Name}/**",
        "Directory.Packages.props"
    };

    workflow.On
        .WorkflowDispatch();
    workflow.On
        .Push()
        .Paths(paths);
    workflow.On
        .PullRequest()
        .Paths(paths);

    workflow.EnvDefaults();

    var job = workflow
        .Job("build")
        .RunEitherOnBranchOrAsPR()
        .Name("Build")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", system.Name)
        .Job;

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    job.TimeoutMinutes(15);

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    job.StepBuild(system.Solution);

    // Devcerts are needed because some tests run start an a http server with https. 
    job.StepDotNetDevCerts();

    job.StepInstallPlayWright();

    job.StepTest(system.Solution);

    job.StepToolRestore();

    job.StepPackSolution(system.Solution);

    job.StepSign();

    job.StepPushToMyGet();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(system.Name);

    var fileName = $"{system.Name}-ci";
    WriteWorkflow(workflow, fileName);
}
void GenerateBffReleaseWorkflow(SystemDescription system)
{
    var workflow = new Workflow($"{system.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .Inputs(new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"));

    workflow.EnvDefaults();

    var job = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", system.Name).Job;

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    job.Step()
        .Name("Git tag")
        .Run($@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""
git tag -a {system.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {system.TagPrefix}-{contexts.Event.Input.Version}");

    job.StepPackSolution(system.Solution);

    job.StepToolRestore();

    job.StepSign();

    job.StepPushToMyGet();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(system.Name);

    var publishJob = workflow.Job("publish")
        .Name("Publish to nuget.org")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Needs("tag")
        .Environment("nuget.org", "");

    publishJob.Step()
        .Uses("actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16") // 4.1.8
        .With(("name", "artifacts"), ("path", "artifacts"));

    publishJob.StepSetupDotNet();

    publishJob.Step()
        .Name("List files")
        .Shell("bash")
        .Run("tree");

    publishJob.StepPushToNuget();

    var fileName = $"{system.Name}-release";
    WriteWorkflow(workflow, fileName);
}


void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

record SystemDescription(string Name, string Solution, string TagPrefix);

public static class StepExtensions
{
    public static void EnvDefaults(this Workflow workflow)
        => workflow.Env(
            ("DOTNET_NOLOGO", "true"),
            ("DOTNET_CLI_TELEMETRY_OPTOUT", "true"));

    public static void StepSetupDotNet(this Job job)
        => job.Step()
            .Name("Setup .NET")
            .ActionsSetupDotNet("3e891b0cb619bf60e2c25674b222b8940e2c1c25", ["6.0.x", "8.0.x", "9.0.x"]); // v4.1.0

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

    public static void StepPackSolution(this Job job, string solution)
    {
        job.Step()
            .Name($"Pack {solution}")
            .Run($"dotnet pack -c Release {solution} -o artifacts");
    }

    public static Step StepBuild(this Job job, string solution)
        => job.Step()
            .Name("Build")
            .Run($"dotnet build {solution} -c Release");

    public static void StepTest(this Job job, string solution)
    {
        var logFileName = "Tests.trx";
        var loggingFlags = $"--logger \"console;verbosity=normal\" "      +
                    $"--logger \"trx;LogFileName={logFileName}\" " +
                    $"--collect:\"XPlat Code Coverage\"";

        job.Step()
            .Name("Test")
            .Run($"dotnet test {solution} -c Release --no-build {loggingFlags}");

        job.Step()
            .Name("Test report")
            .WorkingDirectory("test")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If("github.event == 'push' && (success() || failure())")
            .With(
                ("name", "Test Report"),
                ("path", "**/Tests.trx"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));
    }


    public static Step StepPushToMyGet(this Job job)
        => job.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");
    public static Step StepPushToNuget(this Job job)
        => job.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY");

    public static Step StepPushToGithub(this Job job, GitHubContexts contexts)
        => job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
            .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
                ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));


    public static Step StepSign(this Job job)
    {
        var flags = "--file-digest sha256 "                                                +
                    "--timestamp-rfc3161 http://timestamp.digicert.com "                   +
                    "--azure-key-vault-url https://duendecodesigninghsm.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 "    +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 "    +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} "     +
                    "--azure-key-vault-certificate NuGetPackageSigning";
        return job.Step()
            .Name("Sign packages")
            .IfGithubEventIsPush()
            .Run($"""
                 for file in artifacts/*.nupkg; do
                    dotnet NuGetKeyVaultSignTool sign "$file" {flags}
                 done
                 """);
    }

    public static Step StepPush(this Job job, string destination, string sourceUrl, string secretName)
    {
        var apiKey = $"${{{{ secrets.{secretName} }}}}";
        return job.Step()
            .Name($"Push packages to {destination}")
            .IfRefMain()
            .Run($"dotnet nuget push artifacts/*.nupkg --source {sourceUrl} --api-key {apiKey} --skip-duplicate");
    }

    public static Step StepUploadArtifacts(this Job job, string componentName)
    {
        var path = $"{componentName}/artifacts/*.nupkg";
        return job.Step()
            .Name("Upload Artifacts")
            .IfRefMain()
            .Uses("actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882") // 4.4.3
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
            "(github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name != github.repository) || (github.event_name == 'push')");
}

public class GitHubContexts
{
    public static  GitHubContexts Instance { get; } = new();
    public virtual GitHubContext  GitHub   { get; } = new();
    public virtual SecretsContext Secrets  { get; } = new();
    public virtual EventContext   Event    { get; } = new();

    public abstract class Context(string name)
    {
        protected string Name => name;

        protected string Expression(string s) => "${{ " + s + " }}";
    }

    public class GitHubContext() : Context("github")
    {
    }

    public class SecretsContext() : Context("secrets")
    {
        public string GitHubToken => Expression($"{Name}.GITHUB_TOKEN");
    }

    public class EventContext() : Context("github.event")
    {
        public EventsInputContext Input { get; } = new ();
    }

    public class EventsInputContext() : Context("github.event.inputs")
    {
        public string Version => Expression($"{Name}.version");
    }
}
