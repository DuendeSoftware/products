// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Duende.Conformance.Infrastructure;

/// <summary>
/// Shared fixture that manages the conformance suite lifecycle (Docker Compose),
/// creates the test plan, and provides shared resources (API client, login automation)
/// to all test classes in the collection.
/// </summary>
public abstract class ConformanceSuiteFixture : IAsyncLifetime
{
    /// <summary>
    /// Maximum time to wait for the conformance suite to become available after
    /// starting Docker Compose.
    /// </summary>
    public static readonly TimeSpan ConformanceSuiteStartupTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How often to poll the conformance suite health endpoint during startup.
    /// </summary>
    public static readonly TimeSpan ConformanceSuiteStartupPollingInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum time to wait for a Docker Compose command to complete.
    /// </summary>
    public static readonly TimeSpan DockerComposeTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The URL of the conformance suite (external, accessible from the test host).
    /// </summary>
    private const string ConformanceSuiteUrl = "https://localhost:8443";

    internal const string IdpServiceName = "identity-server";

    /// <summary>
    /// Path to the test-plan-config.json file for this suite.
    /// </summary>
    protected abstract string PlanConfigFilePath { get; }

    /// <summary>
    /// Path to the shared docker-compose.yml file, resolved from the repository root.
    /// </summary>
    private static string ComposeFilePath => Path.Combine(RepoRoot, "conformance", "docker-compose.yml");

    public ConformanceSuiteClient Client { get; private set; } = null!;
    public string PlanId { get; private set; } = null!;
    public string PlanName { get; private set; } = null!;
    public IReadOnlyList<TestModule> Modules { get; private set; } = [];

    public LoginAutomation LoginAutomation { get; private set; } = null!;

    /// <summary>
    /// Tracks whether this fixture started the containers (true) or found them
    /// already running (false). When true, containers are torn down on dispose.
    /// </summary>
    private bool _managedCompose;

    /// <summary>
    /// Resolves the container engine to use. Checks CONTAINER_ENGINE env var first,
    /// then auto-detects: prefers podman if on PATH, falls back to docker.
    /// </summary>
    private static string ContainerEngine =>
        Environment.GetEnvironmentVariable("CONTAINER_ENGINE")
        ?? (IsOnPath("podman") ? "podman" : "docker");

    private static bool IsOnPath(string executable)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            executable += ".exe";
        }

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths.Any(dir => File.Exists(Path.Combine(dir, executable)));
    }

    /// <summary>
    /// Lazily resolved repository root directory. Walks up from AppContext.BaseDirectory
    /// looking for the .git directory.
    /// </summary>
    private static readonly Lazy<string> LazyRepoRoot = new(() =>
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root (.git directory) starting from " + AppContext.BaseDirectory);
    });

    private static string RepoRoot => LazyRepoRoot.Value;

    /// <summary>
    /// Path to the pre-published IdentityServer conformance host output that the
    /// Docker image copies at build time (via the Dockerfile <c>COPY</c> instruction).
    /// The Dockerfile is intentionally runtime-only so <c>dotnet publish</c> runs on
    /// the host with access to the Duende GitHub Packages feed (via user-level
    /// NuGet.Config); the container only needs to run the published output.
    /// </summary>
    private static string HostPublishDirectory => Path.Combine(
        RepoRoot,
        "libs", "identity-server", "test", "IdentityServer.Conformance", "Host", "publish");

    private static string HostProjectPath => Path.Combine(
        RepoRoot,
        "libs", "identity-server", "test", "IdentityServer.Conformance", "Host",
        "Host.IdentityServer.Conformance.csproj");

    public async ValueTask InitializeAsync()
    {
        // Start containers if they aren't already running for this compose project
        if (!await AreContainersRunningAsync())
        {
            await EnsureHostPublishedAsync();
            await RunDockerComposeAsync("up", "--build", "-d");
            _managedCompose = true;
        }

        await WaitForConformanceSuiteAsync();

        Client = new ConformanceSuiteClient(ConformanceSuiteUrl);
        LoginAutomation = await LoginAutomation.CreateAsync();

        // Create the test plan
        var configJson = await File.ReadAllTextAsync(PlanConfigFilePath);
        var config = JsonSerializer.Deserialize<JsonElement>(configJson);

        var planName = config.GetProperty("planName").GetString()!;
        var variant = config.GetProperty("variant").GetRawText();
        var configuration = config.GetProperty("configuration");

        PlanName = planName;
        PlanId = await Client.CreateTestPlanAsync(planName, variant, configuration);
        Modules = await Client.GetTestModulesAsync(PlanId);
    }

    public async ValueTask DisposeAsync()
    {
        if (LoginAutomation != null)
        {
            await LoginAutomation.DisposeAsync();
        }

        Client?.Dispose();

        if (_managedCompose)
        {
            try
            {
                await RunDockerComposeAsync("down");
            }
            catch
            {
                // Best-effort teardown — don't fail the test run
            }
        }
    }

    /// <summary>
    /// Checks whether the containers for this compose project are running by
    /// inspecting the output of <c>docker/podman compose ps</c>.
    /// </summary>
    private async Task<bool> AreContainersRunningAsync()
    {
        try
        {
            var output = await RunDockerComposeAsync("ps", "--format", "json");
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            // podman outputs a JSON array; docker may output one object per line.
            // Try parsing as an array first, then fall back to line-by-line.
            var containers = new List<JsonElement>();
            try
            {
                var array = JsonSerializer.Deserialize<JsonElement>(output);
                if (array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in array.EnumerateArray())
                    {
                        containers.Add(item);
                    }
                }
                else
                {
                    containers.Add(array);
                }
            }
            catch (JsonException)
            {
                // Fall back to one JSON object per line (docker compose format)
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        containers.Add(JsonSerializer.Deserialize<JsonElement>(line));
                    }
                    catch (JsonException)
                    {
                        // Skip non-JSON lines
                    }
                }
            }

            var hasConformance = false;
            var hasIdp = false;

            foreach (var container in containers)
            {
                var state = container.TryGetProperty("State", out var s)
                    ? s.GetString()
                    : null;

                if (state != "running")
                {
                    continue;
                }

                // The service name is in Labels["com.docker.compose.service"] (podman)
                // or in the "Service" property (docker compose)
                var service = GetServiceName(container);

                if (service == "conformance")
                {
                    hasConformance = true;
                }

                if (service == IdpServiceName)
                {
                    hasIdp = true;
                }
            }

            return hasConformance && hasIdp;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetServiceName(JsonElement container)
    {
        // Docker compose: top-level "Service" property
        if (container.TryGetProperty("Service", out var svc))
        {
            return svc.GetString();
        }

        // Podman: Labels["com.docker.compose.service"]
        if (container.TryGetProperty("Labels", out var labels) &&
            labels.TryGetProperty("com.docker.compose.service", out var labelSvc))
        {
            return labelSvc.GetString();
        }

        return null;
    }

    /// <summary>
    /// Publishes the IdentityServer conformance host to <see cref="HostPublishDirectory"/>.
    /// Runs unconditionally so local edits to the host project are always reflected in the
    /// image: <c>dotnet publish</c> is incremental, so re-running when nothing changed is
    /// cheap. In CI the workflow already publishes with authenticated NuGet access; this
    /// call then no-ops in incremental mode.
    /// </summary>
    /// <remarks>
    /// Publishing on the host (rather than inside the Docker build) is deliberate:
    /// the host has the user-level NuGet.Config that authenticates against the
    /// Duende GitHub Packages feed. The Docker image only copies the published
    /// output and does not restore packages.
    /// </remarks>
    private async Task EnsureHostPublishedAsync()
    {
        Directory.CreateDirectory(HostPublishDirectory);

        var psi = new ProcessStartInfo("dotnet", new[]
        {
            "publish",
            HostProjectPath,
            "-c", "Release",
            "-o", HostPublishDirectory,
            "--no-self-contained",
            "-p:TreatWarningsAsErrors=false",
            "-p:GenerateDocumentationFile=false",
        })
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepoRoot,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet publish process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(DockerComposeTimeout);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Process exited between HasExited check and Kill — nothing to do.
                }
            }
        }

        // Always drain stdout/stderr, even on timeout, so the tasks are observed and
        // diagnostics are surfaced in the exception message.
        string stdout;
        string stderr;
        try
        {
            stdout = await stdoutTask;
        }
        catch (Exception ex)
        {
            stdout = $"<stdout read failed: {ex.Message}>";
        }
        try
        {
            stderr = await stderrTask;
        }
        catch (Exception ex)
        {
            stderr = $"<stderr read failed: {ex.Message}>";
        }

        if (timedOut)
        {
            throw new TimeoutException(
                $"dotnet publish timed out after {DockerComposeTimeout.TotalSeconds}s:{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish failed (exit {process.ExitCode}):{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}");
        }
    }

    private async Task<string> RunDockerComposeAsync(params string[] args)
    {
        var engine = ContainerEngine;
        var arguments = new List<string> { "compose", "-f", ComposeFilePath };
        arguments.AddRange(args);

        var psi = new ProcessStartInfo(engine, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {engine} process");

        // Read stdout and stderr concurrently to avoid deadlocks when pipe buffers fill
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(DockerComposeTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"{engine} compose {string.Join(" ", args)} timed out after {DockerComposeTimeout.TotalSeconds}s");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{engine} compose {string.Join(" ", args)} failed (exit {process.ExitCode}):{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}");
        }

        return stdout;
    }

    /// <summary>
    /// Retrieves recent logs from a Docker Compose service.
    /// </summary>
    /// <param name="serviceName">The service name as defined in docker-compose.yml (e.g., "is-oidccore").</param>
    /// <param name="since">Optional start time to filter logs.</param>
    /// <param name="tail">Number of lines to retrieve from the end of the log.</param>
    public async Task<string> GetServiceLogsAsync(string serviceName, DateTime? since = null, int tail = 200)
    {
        var engine = ContainerEngine;
        var arguments = new List<string>
        {
            "compose", "-f", ComposeFilePath,
            "logs"
        };

        if (since.HasValue)
        {
            arguments.Add("--since");
            arguments.Add(since.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            arguments.Add("--tail");
            arguments.Add(tail.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        arguments.Add(serviceName);

        var psi = new ProcessStartInfo(engine, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {engine} process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"{engine} compose logs timed out for service {serviceName}");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        // podman may write logs to stderr in some configurations
        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }

    private async Task WaitForConformanceSuiteAsync(CancellationToken ct = default)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var http = new HttpClient(handler);

        var deadline = DateTime.UtcNow + ConformanceSuiteStartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await http.GetAsync($"{ConformanceSuiteUrl}/api/runner/available", ct);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Not ready yet
            }

            await Task.Delay(ConformanceSuiteStartupPollingInterval, ct);
        }

        throw new TimeoutException("Conformance suite did not become available within the timeout period");
    }
}
