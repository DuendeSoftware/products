// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Logicality.GitHub.Actions.Workflow;
using static GitHubContexts;

var contexts = Instance;

{
    Product identityServer = new("identity-server", "identity-server.slnf", "is");
    GenerateIdentityServerWorkflow(identityServer);
    GenerateIdentityServerReleaseWorkflow(identityServer);
    GenerateCodeQlWorkflow(identityServer, "38 15 * * 0");
    
    GenerateTemplatesReleaseWorkflow(identityServer);
}

{
    Product bff = new("bff", "bff.slnf", "bff");
    GenerateBffWorkflow(bff);
    GenerateBffReleaseWorkflow(bff);
    GenerateCodeQlWorkflow(bff, "38 16 * * 0");

    GenerateTemplatesReleaseWorkflow(bff);
}


void GenerateIdentityServerWorkflow(Product product)
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

    job.StepBuild(product.Solution);

    job.StepTest(product.Solution);

    job.StepToolRestore();

    job.StepPack(product.Solution);

    job.StepPack(product.TemplateProject);

    job.StepSign();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(product.Name);

    var fileName = $"{product.Name}-ci";
    WriteWorkflow(workflow, fileName);
}
void GenerateIdentityServerReleaseWorkflow(Product product)
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

    job.Step()
        .Name("Git tag")
        .Run($@"git config --global user.email ""github-bot@duendesoftware.com""
git config --global user.name ""Duende Software GitHub Bot""
git tag -a {product.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
git push origin {product.TagPrefix}-{contexts.Event.Input.Version}");

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

void GenerateBffWorkflow(Product product)
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

    job.StepBuild(product.Solution);

    // Devcerts are needed because some tests run start an a http server with https. 
    job.StepDotNetDevCerts();

    job.StepInstallPlayWright();

    job.StepTest(product.Solution);

    job.StepUploadPlaywrightTestTraces(product.Name);

    job.StepToolRestore();

    job.StepPack(product.Solution);

    job.StepPack(product.TemplateProject);


    job.StepSign();

    job.StepPushToGithub(contexts);

    job.StepUploadArtifacts(product.Name);

    var fileName = $"{product.Name}-ci";
    WriteWorkflow(workflow, fileName);
}
void GenerateBffReleaseWorkflow(Product product)
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
    job.StepPack(product.TemplateProject);

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
    
    job.StepAutoBuildCodeQl();
    
    job.StepPerformCodeQlAnalysis();
    
    var fileName = $"{system.Name}-codeql-analysis";
    WriteWorkflow(workflow, fileName);
}

void GenerateTemplatesReleaseWorkflow(Product product)
{
    var workflow = new Workflow($"{product.Name}/templates/release");

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

    job.StepPack(product.TemplateProject);

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

    var fileName = $"{product.Name}-templates-release";
    WriteWorkflow(workflow, fileName);
}

void WriteWorkflow(Workflow workflow, string fileName)
{
    var filePath = $"../workflows/{fileName}.yml";
    workflow.WriteYaml(filePath);
    Console.WriteLine($"Wrote workflow to {filePath}");
}

record Product(string Name, string Solution, string TagPrefix)
{
    public string TemplateProject => $"templates/templates.{Name}.csproj";
}

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

    public static void StepPack(this Job job, string target)
    {
        job.Step()
            .Name($"Pack {target}")
            .Run($"dotnet pack -c Release {target} -o artifacts");
    }

    public static Step StepBuild(this Job job, string solution)
        => job.Step()
            .Name("Build")
            .Run($"dotnet build {solution} -c Release");

    public static void StepTest(this Job job, string solution)
    {
        var logFileName = "Tests.trx";
        var loggingFlags = $"--logger \"console;verbosity=normal\" " +
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

    public static Step StepPushToNuget(this Job job, bool pushAlways = false)
        => job.StepPush("nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_API_KEY", pushAlways);

    public static Step StepPushToGithub(this Job job, GitHubContexts contexts, bool pushAlways = false)
        => job.StepPush("GitHub", "https://nuget.pkg.github.com/DuendeSoftware/index.json", "GITHUB_TOKEN", pushAlways)
            .Env(("GITHUB_TOKEN", contexts.Secrets.GitHubToken),
                ("NUGET_AUTH_TOKEN", contexts.Secrets.GitHubToken));


    public static void StepSign(this Job job, bool always = false)
    {
        var flags = "--file-digest sha256 "                                                +
                    "--timestamp-rfc3161 http://timestamp.digicert.com "                   +
                    "--azure-key-vault-url https://duendecodesigninghsm.vault.azure.net/ " +
                    "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 "    +
                    "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 "    +
                    "--azure-key-vault-client-secret ${{ secrets.SignClientSecret }} "     +
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

    public static Step StepGitCheckoutCustomBranch(this Job job)
    {
        return job.Step()
            .Name("Checkout target branch")
            .If("github.event.inputs.branch != 'main'")
            .Run("git checkout ${{ github.event.inputs.branch }}");
    }

    public static Step StepGitConfig(this Job job)
    {
        return job.Step()
            .Name("Git Config")
            .Run("""
                 git config --global user.email "github-bot@duendesoftware.com"
                 git config --global user.name "Duende Software GitHub Bot"
                 """);
    }
    internal static Step StepGitRemoveExistingTagIfConfigured(this Job job, Product component, GitHubContexts contexts)
    {
        return job.Step()
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
    }
    internal static Step StepGitPushTag(this Job job, Product component, GitHubContexts contexts)
    {
        return job.Step()
            .Name("Git Config")
            .Run($"""
                 git tag -a {component.TagPrefix}-{contexts.Event.Input.Version} -m ""Release v{contexts.Event.Input.Version}""
                 git push origin {component.TagPrefix}-{contexts.Event.Input.Version}
                 """);
    }

    public static WorkflowDispatch InputVersionBranchAndTagOverride(this WorkflowDispatch workflow)
    {
        return workflow.Inputs(
            new StringInput("version", "Version in format X.Y.Z or X.Y.Z-preview.", true, "0.0.0"),
            new StringInput("branch", "(Optional) the name of the branch to release from", false, "main"),
            new BooleanInput("remove-tag-if-exists", "If set, will remove the existing tag. Use this if you have issues with the previous release action", false, false));
    }

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
            "(github.event_name == 'pull_request' && github.event.pull_request.head.repo.full_name != github.repository) || (github.event_name == 'push')");
    
    public static void StepInitializeCodeQl(this Job job)
    {
        job.Step()
            .Name("Initialize CodeQL")
            .Uses("github/codeql-action/init@9e8d0789d4a0fa9ceb6b1738f7e269594bdd67f0") // 3.28.9
            .With(
                ("languages", "csharp"));
    }
    
    public static void StepAutoBuildCodeQl(this Job job)
    {
        job.Step()
            .Name("Auto Build")
            .Uses("github/codeql-action/autobuild@9e8d0789d4a0fa9ceb6b1738f7e269594bdd67f0"); // 3.28.9
    }
    
    public static void StepPerformCodeQlAnalysis(this Job job)
    {
        job.Step()
            .Name("Perform CodeQL Analysis")
            .Uses("github/codeql-action/analyze@9e8d0789d4a0fa9ceb6b1738f7e269594bdd67f0") // 3.28.9
            .With(
                ("category", "/language:csharp"));
    }
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
