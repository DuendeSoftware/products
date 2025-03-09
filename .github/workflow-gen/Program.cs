// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;

var contexts = GitHubContexts.Instance;

var products = new Product[]
{
    new("aspnetcore-authentication-jwtbearer", "aspnetcore-authentication-jwtbearer.slnf", "aaj"),
    new("identity-server", "identity-server.slnf", "is"),
    new("bff", "bff.slnf", "bff", true)
};
foreach (var product in products)
{
    GenerateCiWorkflow(product);
    GenerateReleaseWorkflow(product);
    GenerateCodeQlWorkflow(product, "38 15 * * 0");
}

GenerateTemplatesReleaseWorkflow(new Product("templates", "../artifacts/templates.csproj", "templates"));

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

    var job = workflow
        .Job("build")
        .RunEitherOnBranchOrAsPR()
        .Name("Build")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", product.Name)
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

    job.StepRestore(product.Solution);

    job.StepVerifyFormatting(product.Solution);

    job.StepBuild(product.Solution);

    // Devcerts are needed because some tests run start a http server with https. 
    job.StepDotNetDevCerts();

    if (product.EnablePlaywright)
    {
        job.StepInstallPlayWright();
    }

    job.StepTest(product.Solution);

    if (product.EnablePlaywright)
    {
        job.StepUploadPlaywrightTestTraces(product.Name);
    }

    job.StepToolRestore();

    job.StepPack(product.Solution);

    job.StepSign();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(product.Name);

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

void GenerateCodeQlWorkflow(Product system, string cronSchedule)
{
    var workflow = new Workflow($"{system.Name}/codeql");
    var branches = new[] { "main" };
    var paths = new[] { $"{system.Name}/**" };

    workflow.On
        .WorkflowDispatch();
    workflow.On
        .Push()
        .Branches(branches)
        .Paths(paths);
    workflow.On
        .PullRequest()
        .Paths(paths);
    workflow.On
        .Schedule(cronSchedule);

    var job = workflow
        .Job("analyze")
        .Name("Analyze")
        .RunsOn(GitHubHostedRunners.UbuntuLatest)
        .Defaults().Run("bash", system.Name)
        .Job;

    job.Permissions(
        actions: Permission.Read,
        contents: Permission.Read,
        securityEvents: Permission.Write);

    job.Step()
        .ActionsCheckout();

    job.StepInitializeCodeQl();

    job.StepSetupDotNet();

    job.StepRestore(system.Solution);

    job.StepBuild(system.Solution);

    job.StepPerformCodeQlAnalysis();

    var fileName = $"{system.Name}-codeql-analysis";
    WriteWorkflow(workflow, fileName);
}

void GenerateTemplatesReleaseWorkflow(Product product)
{
    var workflow = new Workflow($"{product.Name}/release");

    workflow.On
        .WorkflowDispatch()
        .Inputs(new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"));

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

    job.Step()
        .Name("build templates")
        .Run("dotnet run --project build");

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

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

record Product(string Name, string Solution, string TagPrefix, bool EnablePlaywright = false);
