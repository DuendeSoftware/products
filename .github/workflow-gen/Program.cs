// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;
using static GitHubContexts;

var contexts = Instance;
Component[] components = [
    new("bff",
        ["Duende.Bff", "Duende.Bff.Blazor", "Duende.Bff.Blazor.Client", "Duende.Bff.EntityFramework", "Duende.Bff.Yarp"],
        ["Duende.Bff.Tests", "Duende.Bff.EntityFramework.Tests", "Duende.Bff.Blazor.UnitTests", "Duende.Bff.Blazor.Client.UnitTests"],
        "bff"),

    new("identity-server", 
        ["AspNetIdentity", "Configuration", "Configuration.EntityFramework", "EntityFramework", "EntityFramework.Storage", "IdentityServer", "Storage"],
        ["Configuration.IntegrationTests", "EntityFramework.IntegrationTests", "EntityFramework.Storage.IntegrationTests", "EntityFramework.Storage.UnitTests", "IdentityServer.IntegrationTests", "IdentityServer.UnitTests"],
        "is")
];

foreach (var component in components)
{
    GenerateCiWorkflow(component);
    GenerateReleaseWorkflow(component);
}

void GenerateCiWorkflow(Component component)
{
    var workflow = new Workflow($"{component.Name}/ci");
    var paths    = new[]
    {
        $".github/workflows/{component.Name}-**", 
        $"{component.Name}/**",
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
        .Defaults().Run("bash", component.Name)
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

    foreach (var testProject in component.Tests)
    {
        job.StepTestAndReport(component.Name, testProject);
    }

    job.StepToolRestore();

    foreach (var project in component.Projects)
    {
        job.StepPack(project);
    }

    job.StepSign()
        .OnlyIfOnBranch()
        ;

    job.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    job.StepUploadArtifacts(component.Name);

    var fileName = $"{component.Name}-ci";
    WriteWorkflow(workflow, fileName);
}

void GenerateReleaseWorkflow(Component component)
{
    var workflow = new Workflow($"{component.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .Inputs(new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"));

    workflow.EnvDefaults();

    var tagJob = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", component.Name).Job;

    tagJob.Step()
        .ActionsCheckout();

    tagJob.StepSetupDotNet();

    tagJob.Step()
        .Name("Git tag")
        .Run($@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""
git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {component.TagPrefix}-{contexts.Event.Input.Version}");

    foreach (var project in component.Projects)
    {
        tagJob.StepPack(project);
    }

    tagJob.StepToolRestore();

    tagJob.StepSign();

    tagJob.StepPush("MyGet", "https://www.myget.org/F/duende_identityserver/api/v2/package", "MYGET");

    tagJob.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN")
        .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
            ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));

    tagJob.StepUploadArtifacts(component.Name);

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

    publishJob.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY");

    var fileName = $"{component.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

record Component(string Name, string[] Projects, string[] Tests, string TagPrefix);

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
    public static Step OnlyIfOnBranch(this Step step)
        => step.If("github.event == 'push'");

    public static void StepTestAndReport(this Job job, string componentName, string testProject)
    {
        var path        = $"test/{testProject}";
        var logFileName = "Tests.trx";
        var flags = $"--logger \"console;verbosity=normal\" "      +
                    $"--logger \"trx;LogFileName={logFileName}\" " +
                    $"--collect:\"XPlat Code Coverage\"";
        job.Step()
            .Name($"Test - {testProject}")
            .Run($"dotnet test -c Release {path} {flags}");

        job.Step()
            .Name($"Test report - {testProject}")
            .Uses("dorny/test-reporter@31a54ee7ebcacc03a09ea97a7e5465a47b84aea5") // v1.9.1
            .If("success() || failure()")
            .With(
                ("name", $"Test Report - {testProject}"),
                ("path", $"{componentName}/{path}/TestResults/{logFileName}"),
                ("reporter", "dotnet-trx"),
                ("fail-on-error", "true"),
                ("fail-on-empty", "true"));
    }

    public static void StepToolRestore(this Job job)
        => job.Step()
            .Name("Tool restore")
            .Run("dotnet tool restore");

    public static void StepPack(this Job job, string project)
    {
        var path = $"src/{project}";
        job.Step()
            .Name($"Pack {project}")
            .Run($"dotnet pack -c Release {path} -o artifacts");
    }

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
            .OnlyIfOnBranch()
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
