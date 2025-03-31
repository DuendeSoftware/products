// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;

var contexts = GitHubContexts.Instance;

var products = new Product[]
{
    new("aspnetcore-authentication-jwtbearer",
        "aspnetcore-authentication-jwtbearer.slnf",
        "aaj",
        ["AspNetCore.Authentication.JwtBearer.Tests"],
        []),
    new("bff",
        "bff.slnf",
        "bff",
        ["Bff.Tests"],
        ["Hosts.Tests"]),
    new("identity-server",
        "identity-server.slnf",
        "is",
        ["Configuration.IntegrationTests", "EntityFramework.IntegrationTests", "EntityFramework.Storage.UnitTests", "IdentityServer.IntegrationTests", "IdentityServer.UnitTests"],
        [])
};
foreach (var product in products)
{
    GenerateCiWorkflow(product);
    GenerateReleaseWorkflow(product);
}

GenerateTemplatesReleaseWorkflow(new Product("templates", "../artifacts/templates.csproj", "templates", [], []));


void GenerateCiWorkflow(Product product)
{
    var workflow = new Workflow($"{product.Name}/ci");
    var paths = new[]
    {
        $".github/workflows/{product.Name}-**",
        $"{product.Name}/**",
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

    const string VerifyFormattingJobId = "verify-formatting";
    const string CodeQlJobId = "codeql";
    const string PlaywrightJobId = "playwright";
    const string BuildJobId = "build";

    // Verify formatting
    var verifyFormattingJob = workflow
        .Job(VerifyFormattingJobId)
        .RunEitherOnBranchOrAsPR()
        .Name("Verify formatting")
        .RunsOn("large", ["ubuntu-latest-x64-16core"])
        .Defaults().Run("bash", product.Name)
        .Job;

    verifyFormattingJob.Permissions(contents: Permission.Read);

    verifyFormattingJob.TimeoutMinutes(15);

    verifyFormattingJob.Step()
        .ActionsCheckout();

    verifyFormattingJob.StepSetupDotNet();

    verifyFormattingJob.StepRestore(product.Solution);

    verifyFormattingJob.StepVerifyFormatting(product.Solution);

    // Build
    var build = workflow
        .Job(BuildJobId)
        .RunEitherOnBranchOrAsPR()
        .Name("Build and test (unit)")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", product.Name)
        .Job;

    build.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write,
        packages: Permission.Write);

    build.TimeoutMinutes(15);

    build.Step()
        .ActionsCheckout();

    build.StepSetupDotNet();

    build.StepRestore(product.Solution);

    build.StepBuild(product.Solution);

    build.StepDotNetDevCerts();

    foreach (var project in product.UnitTestProjects)
    {
        build.StepTest($"test/{project}");
    }

    // Playwright
    var playwrightJob = workflow
        .Job(PlaywrightJobId)
        .RunEitherOnBranchOrAsPR()
        .Name("Playwright tests")
        .RunsOn("large", ["ubuntu-latest-x64-16core"])
        .Defaults().Run("bash", product.Name)
        .Job;

    playwrightJob.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        checks: Permission.Write);

    playwrightJob.TimeoutMinutes(15);

    playwrightJob.Step()
        .ActionsCheckout();

    if (product.PlaywrightTestProjects.Length > 0)
    {
        playwrightJob.StepSetupDotNet();

        playwrightJob.StepRestore(product.Solution);

        playwrightJob.StepBuild(product.Solution);

        playwrightJob.StepInstallPlayWright();

        playwrightJob.StepDotNetDevCerts();

        foreach (var project in product.PlaywrightTestProjects)
        {
            playwrightJob.StepTest($"test/{project}");
        }

        playwrightJob.StepUploadPlaywrightTestTraces(product.Name);
    }

    // CodeQL
    var codeQlJob = workflow
        .Job(CodeQlJobId)
        .RunEitherOnBranchOrAsPR()
        .Name("CodeQL analyze")
        .RunsOn("large", ["ubuntu-latest-x64-16core"])
        .Defaults().Run("bash", product.Name)
        .Job;

    codeQlJob.Step()
        .ActionsCheckout();

    codeQlJob.StepInitializeCodeQl();

    codeQlJob.StepSetupDotNet();

    codeQlJob.StepRestore(product.Solution);

    codeQlJob.StepBuild(product.Solution);

    codeQlJob.StepPerformCodeQlAnalysis();

    // Pack
    var packJob = workflow
        .Job("pack")
        .RunEitherOnBranchOrAsPR()
        .Name("Pack, sign and push")
        .RunsOn("large", ["ubuntu-latest-x64-16core"])
        .Needs(VerifyFormattingJobId, BuildJobId, PlaywrightJobId, CodeQlJobId)
        .Defaults().Run("bash", product.Name)
        .Job;

    packJob.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        packages: Permission.Write);

    packJob.TimeoutMinutes(15);

    packJob.Step()
        .ActionsCheckout();

    packJob.StepSetupDotNet();

    packJob.StepToolRestore();

    packJob.StepPack(product.Solution);

    packJob.StepSign();

    packJob.StepPushToGithub(contexts);

    packJob.StepUploadArtifacts(product.Name);

    var fileName = $"{product.Name}-ci";
    WriteWorkflow(workflow, fileName);
}

void GenerateReleaseWorkflow(Product product)
{
    var workflow = new Workflow($"{product.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .InputVersionBranchAndTagOverride();

    workflow.EnvDefaults();

    var job = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", product.Name).Job;

    job.Step()
        .ActionsCheckout();

    job.StepGitCheckoutCustomBranch();
    job.StepGitConfig();
    job.StepGitRemoveExistingTagIfConfigured(product, contexts);
    job.StepGitPushTag(product, contexts);

    job.StepSetupDotNet();

    job.StepPack(product.Solution);

    job.StepToolRestore();

    job.StepSign(always: true);

    job.StepPushToGithub(contexts, pushAlways: true);

    job.StepUploadArtifacts(product.Name, uploadAlways: true);

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

    publishJob.StepPushToNuget(pushAlways: true);

    var fileName = $"{product.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void GenerateTemplatesReleaseWorkflow(Product product)
{
    var workflow = new Workflow($"{product.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .InputVersionBranchAndTagOverride();

    workflow.EnvDefaults();

    var job = workflow
        .Job("tag")
        .Name("Tag and Pack")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Permissions(contents: Permission.Write, packages: Permission.Write)
        .Defaults().Run("bash", product.Name).Job;

    job.Step()
        .ActionsCheckout();

    job.StepSetupDotNet();

    job.StepGitCheckoutCustomBranch();
    job.StepGitConfig();
    job.StepGitRemoveExistingTagIfConfigured(product, contexts);
    job.StepGitPushTag(product, contexts);

    job.StepToolRestore();

    job.Step()
        .Name("build templates")
        .Run("dotnet run --project build");

    job.StepPack(product.Solution);

    job.StepSign(always: true);

    job.StepPushToGithub(contexts, pushAlways: true);

    job.StepUploadArtifacts(product.Name, uploadAlways: true);

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

    publishJob.StepPushToNuget(pushAlways: true);

    var fileName = $"{product.Name}-release";
    WriteWorkflow(workflow, fileName);
}

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

internal record Product(string Name, string Solution, string TagPrefix, string[] UnitTestProjects, string[] PlaywrightTestProjects);
