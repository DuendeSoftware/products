// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.Conformance.Infrastructure;

/// <summary>
/// Client for the OIDF Conformance Suite REST API.
/// Handles creating test plans, starting modules, polling for results, and exporting logs.
/// </summary>
public sealed class ConformanceSuiteClient : IDisposable
{
    private readonly HttpClient _http;

    /// <summary>
    /// How long to wait between polling the conformance suite for module status.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Maximum time to wait for a module to reach a terminal state.
    /// </summary>
    public TimeSpan ModuleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public ConformanceSuiteClient(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }

    /// <summary>
    /// Creates a test plan and returns the plan ID.
    /// </summary>
    public async Task<string> CreateTestPlanAsync(string planName, string variant, object configuration, Ct ct = default)
    {
        var variantEncoded = Uri.EscapeDataString(variant);
        var planNameEncoded = Uri.EscapeDataString(planName);
        var url = $"/api/plan?planName={planNameEncoded}&variant={variantEncoded}";

        var response = await _http.PostAsJsonAsync(url, configuration, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Conformance suite returned {(int)response.StatusCode} when creating test plan. " +
                $"URL: {url}{Environment.NewLine}Response: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreatePlanResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty response from conformance suite when creating plan");

        return result.Id;
    }

    /// <summary>
    /// Returns all test modules for a given plan.
    /// </summary>
    public async Task<IReadOnlyList<TestModule>> GetTestModulesAsync(string planId, Ct ct = default)
    {
        var response = await _http.GetAsync($"/api/plan/{planId}", ct);
        response.EnsureSuccessStatusCode();

        var plan = await response.Content.ReadFromJsonAsync<PlanDetails>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Empty response when fetching plan {planId}");

        return plan.Modules ?? [];
    }

    /// <summary>
    /// Starts a test module and returns the module ID.
    /// </summary>
    public async Task<string> StartTestModuleAsync(string planId, string moduleName, Ct ct = default)
    {
        var response = await _http.PostAsync($"/api/runner?plan={planId}&test={moduleName}", null, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<StartModuleResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Empty response when starting module {moduleName}");

        return result.Id;
    }


    /// <summary>
    /// Returns browser-API URLs that the conformance suite wants the browser to visit.
    /// The suite adds a URL to "urls" when it wants a visit; after
    /// <see cref="VisitBrowserUrlAsync"/> is called the URL moves to "visited" and a new
    /// URL may appear for the next required visit.
    /// </summary>
    private async Task<List<string>> GetPendingBrowserUrlsAsync(string moduleId, Ct ct)
    {
        var response = await _http.GetAsync($"/api/runner/browser/{moduleId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            // Browser API may not exist for all modules — treat as no pending URLs
            return [];
        }

        var state = await response.Content.ReadFromJsonAsync<BrowserState>(cancellationToken: ct);
        if (state is null || state.Urls.Count == 0)
        {
            return [];
        }

        return [.. state.Urls];
    }

    /// <summary>
    /// Retrieves the full test log for a module.
    /// </summary>
    public async Task<LogEntry[]> GetTestLogAsync(string moduleId, Ct ct = default)
    {
        var response = await _http.GetAsync($"/api/log/{moduleId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LogEntry[]>(cancellationToken: ct) ?? [];
    }

    /// <summary>
    /// Derives a top-level module result from individual log entries. The conformance suite
    /// records per-check results in the log before setting the module's final result, so this
    /// allows early detection of outcomes while the module is still in WAITING status.
    /// Priority order: REVIEW > WARNING > FAILURE > PASSED (requires FINISHED and no failures).
    /// Returns <c>null</c> if no conclusive result can be determined.
    /// </summary>
    private static string? DeriveResultFromLog(LogEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        // Check for REVIEW or WARNING first (they take precedence over SUCCESS)
        if (entries.Any(e => e.Result == "REVIEW"))
        {
            return "REVIEW";
        }

        if (entries.Any(e => e.Result == "WARNING"))
        {
            return "WARNING";
        }

        // Failed checks must take precedence over FINISHED; the suite can emit FINISHED
        // for runs that complete with failures.
        if (entries.Any(e => e.Result is "FAILURE" or "FAILED"))
        {
            return "FAILURE";
        }

        // A FINISHED entry with no failures means the test ran to completion successfully.
        if (entries.Any(e => e.Result == "FINISHED"))
        {
            return "PASSED";
        }

        return null;
    }

    /// <summary>
    /// Queries the conformance suite API for the current status and result of a module.
    /// Returns null if the module hasn't finished yet or the query fails.
    /// </summary>
    public async Task<ModuleResult?> GetModuleResultAsync(string moduleId, Ct ct = default)
    {
        var response = await _http.GetAsync($"/api/info/{moduleId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var info = await response.Content.ReadFromJsonAsync<ModuleInfo>(cancellationToken: ct);
        if (info?.Status is not ("FINISHED" or "INTERRUPTED"))
        {
            return null;
        }

        return new ModuleResult(moduleId, info.Status, info.Result ?? "UNKNOWN");
    }

    /// <summary>
    /// Signals to the conformance suite that the browser has visited the given URL.
    /// This is required for tests that check whether the browser visited a specific URL
    /// (e.g., par-ensure-reused-request-uri-prior-to-auth-completion-succeeds).
    /// </summary>
    public async Task VisitBrowserUrlAsync(string moduleId, string url, Ct ct = default)
    {
        var encodedUrl = Uri.EscapeDataString(url);
        var response = await _http.PostAsync($"/api/runner/browser/{moduleId}/visit?url={encodedUrl}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Exports the HTML report for a test module, extracts it, and saves the HTML file
    /// to conformance-reports/{planName}/{moduleName}/. Returns the path to the HTML file, or null if export failed.
    /// </summary>
    public async Task<string?> ExportTestHtmlAsync(string moduleId, string planName, string moduleName, Ct ct = default)
    {
        var response = await _http.GetAsync($"/api/log/exporthtml/{moduleId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var outputDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "conformance-reports", planName, moduleName));
        Directory.CreateDirectory(outputDir);

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        using var zipStream = new MemoryStream(bytes);
        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

        string? htmlPath = null;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var extension = Path.GetExtension(entry.Name);

            // Only extract HTML files — skip .json and .sig
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetPath = Path.Combine(outputDir, $"{moduleName}{extension}");
#pragma warning disable CA1849 // ZipArchiveEntry has no OpenAsync method
            using var entryStream = entry.Open();
#pragma warning restore CA1849
            using var fileStream = File.Create(targetPath);
            await entryStream.CopyToAsync(fileStream, ct);

            htmlPath ??= targetPath;
        }

        return htmlPath;
    }

    /// <summary>
    /// Polls a test module until either the module finishes or a URL requires browser
    /// interaction. Returns a <see cref="ConformanceStep"/> describing what happened.
    ///
    /// Call this in a loop from test code: handle the step, then call again until
    /// the returned step is a <see cref="ConformanceModuleDone"/> step.
    /// </summary>
    public async Task<ConformanceStep> WaitForNextStepAsync(
        string moduleId,
        string internalHost,
        string externalHost,
        Action<string>? log = null,
        Ct ct = default)
    {
        var deadline = DateTime.UtcNow + ModuleTimeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var info = await GetModuleInfoAsync(moduleId, ct);
            log?.Invoke($"Module {moduleId} status: {info.Status}, result: {info.Result}");

            if (info.Status is "FINISHED" or "INTERRUPTED")
            {
                return new ConformanceModuleDone(await ResolveResultAsync(moduleId, info, ct));
            }

            if (info.Status == "WAITING")
            {
                var browserUrls = await GetPendingBrowserUrlsAsync(moduleId, ct);
                if (browserUrls.Count > 0)
                {
                    var step = ToBrowserUrlStep(browserUrls[0], internalHost, externalHost);
                    log?.Invoke($"Next step: {step.Kind} browser URL: {step.Url}");
                    return step;
                }

                if (info.Result == "REVIEW")
                {
                    log?.Invoke($"Module {moduleId} is WAITING with REVIEW result (error-page test). Exiting.");
                    return new ConformanceModuleDone(await ResolveResultAsync(moduleId, info, ct));
                }

                if (info.Result is "PASSED" or "WARNING" or "FAILURE" or "FAILED")
                {
                    log?.Invoke($"Module {moduleId} is WAITING with unexpected terminal result '{info.Result}'. Exiting.");
                    return new ConformanceModuleDone(await ResolveResultAsync(moduleId, info, ct));
                }
            }

            await Task.Delay(PollingInterval, ct);
        }

        var finalLog = await GetTestLogAsync(moduleId, ct);
        throw new TimeoutException(
            $"Module {moduleId} did not complete within the timeout period. " +
            $"Last known result from log: {DeriveResultFromLog(finalLog) ?? "none"}.");
    }

    private async Task<ModuleInfo> GetModuleInfoAsync(string moduleId, Ct ct = default)
    {
        var response = await _http.GetAsync($"/api/info/{moduleId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModuleInfo>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Empty response when polling module {moduleId}");
    }

    private async Task<ModuleResult> ResolveResultAsync(string moduleId, ModuleInfo info, Ct ct)
    {
        var result = info.Result;
        if (result is null or "UNKNOWN")
        {
            var testLog = await GetTestLogAsync(moduleId, ct);
            result = DeriveResultFromLog(testLog) ?? "UNKNOWN";
        }

        return new ModuleResult(moduleId, info.Status ?? "UNKNOWN", result);
    }

    private static ConformanceUrlStep ToBrowserUrlStep(string rawUrl, string internalHost, string externalHost)
    {
        var url = rawUrl.Replace(
            $"https://{internalHost}", $"https://{externalHost}",
            StringComparison.OrdinalIgnoreCase);
        var kind = url.Contains("/connect/endsession", StringComparison.OrdinalIgnoreCase)
            ? ConformanceStepKind.EndSession
            : ConformanceStepKind.Authorize;
        return new ConformanceUrlStep(url, rawUrl, kind);
    }

    /// <summary>
    /// Polls until the module reaches a terminal state (FINISHED, INTERRUPTED, or WAITING
    /// with a terminal result). Handles any pending browser-API URLs by navigating to them
    /// using the optional <paramref name="browserUrlHandler"/> (e.g., for prompt=none authorize
    /// requests that need IS to return login_required). If no handler is provided, browser URLs
    /// are ignored and the module is allowed to finish on its own.
    /// </summary>
    public async Task<ModuleResult> WaitForDoneAsync(
        string moduleId,
        string internalHost = "",
        string externalHost = "",
        Func<string, Task>? browserUrlHandler = null,
        Action<string>? log = null,
        Ct ct = default)
    {
        var deadline = DateTime.UtcNow + ModuleTimeout;
        var hasHandledAnyUrl = false;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var info = await GetModuleInfoAsync(moduleId, ct);
            log?.Invoke($"Module {moduleId} status: {info.Status}, result: {info.Result}");

            if (info.Status is "FINISHED" or "INTERRUPTED")
            {
                return await ResolveResultAsync(moduleId, info, ct);
            }

            if (info.Status == "WAITING")
            {
                if (browserUrlHandler != null)
                {
                    var handled = await TryHandleBrowserUrlsAsync(
                        moduleId, internalHost, externalHost, browserUrlHandler, log, ct);

                    if (handled)
                    {
                        hasHandledAnyUrl = true;
                        continue;
                    }
                }

                if (HasTerminalResult(info))
                {
                    LogTerminalResult(moduleId, info, log);
                    return await ResolveResultAsync(moduleId, info, ct);
                }

                if (hasHandledAnyUrl)
                {
                    // All browser URLs have been handled but the module is still WAITING.
                    // Check the log for a terminal result (the suite may not have transitioned yet).
                    // Only trust the log once it contains a FINISHED entry — WARNING/REVIEW entries
                    // can appear mid-run and must not be treated as a terminal result on their own.
                    var testLog = await GetTestLogAsync(moduleId, ct);
                    var logResult = testLog.Any(e => e.Result == "FINISHED")
                        ? DeriveResultFromLog(testLog)
                        : null;
                    if (logResult is "PASSED" or "WARNING" or "REVIEW")
                    {
                        log?.Invoke($"Module {moduleId} log has terminal result '{logResult}' while WAITING. Exiting.");
                        return new ModuleResult(moduleId, "INTERRUPTED", logResult);
                    }
                }
            }

            await Task.Delay(PollingInterval, ct);
        }

        var finalLog = await GetTestLogAsync(moduleId, ct);
        throw new TimeoutException(
            $"Module {moduleId} did not complete within the timeout period. " +
            $"Last known result from log: {DeriveResultFromLog(finalLog) ?? "none"}.");
    }

    /// <summary>
    /// Attempts to handle any pending browser URLs. Returns true if at least one URL was handled.
    /// </summary>
    private async Task<bool> TryHandleBrowserUrlsAsync(
        string moduleId,
        string internalHost,
        string externalHost,
        Func<string, Task> browserUrlHandler,
        Action<string>? log,
        Ct ct)
    {
        var browserUrls = await GetPendingBrowserUrlsAsync(moduleId, ct);
        if (browserUrls.Count == 0)
        {
            return false;
        }

        foreach (var rawUrl in browserUrls)
        {
            var externalUrl = string.IsNullOrEmpty(internalHost)
                ? rawUrl
                : rawUrl.Replace($"https://{internalHost}", $"https://{externalHost}", StringComparison.OrdinalIgnoreCase);

            log?.Invoke($"Navigating to browser URL: {externalUrl}");
            try
            {
                await browserUrlHandler(externalUrl);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Browser URL navigation failed (non-fatal): {ex.Message}");
            }

            await VisitBrowserUrlAsync(moduleId, rawUrl, ct);
        }

        return true;
    }

    private static bool HasTerminalResult(ModuleInfo info) =>
        info.Result is "PASSED" or "WARNING" or "FAILURE" or "REVIEW" or "FAILED";

    private static void LogTerminalResult(string moduleId, ModuleInfo info, Action<string>? log)
    {
        var msg = info.Result == "REVIEW"
            ? $"Module {moduleId} is WAITING with REVIEW result (error-page test). Exiting."
            : $"Module {moduleId} is WAITING with unexpected terminal result '{info.Result}'. Exiting.";
        log?.Invoke(msg);
    }

    /// <summary>
    /// Signals to the conformance suite that a browser-API URL has been visited,
    /// moving it from the pending list to the visited list.
    /// Call this after handling a <see cref="ConformanceUrlStep"/>.
    /// </summary>
    public Task AcknowledgeBrowserUrlAsync(string moduleId, ConformanceUrlStep step, Ct ct = default) =>
        VisitBrowserUrlAsync(moduleId, step.RawUrl, ct);

    /// <summary>
    /// Uploads a placeholder screenshot to the conformance suite for a module that requires
    /// manual review (REVIEW result). This satisfies the suite's image placeholder so the
    /// module can transition from WAITING to FINISHED.
    /// </summary>
    public async Task UploadPlaceholderScreenshotAsync(string moduleId, Ct ct = default)
    {

        var results = await _http.GetFromJsonAsync<ImageResult[]>($"/api/log/{moduleId}/images", ct) ?? [];

        foreach (var result in results)
        {
            // Dummy PNG as a data URI — the conformance suite needs a valid image
            // to satisfy its placeholder check and transition the module to FINISHED.
            const string placeholderImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2NgYGD4DwABBAEAwS2OUAAAABBkZUJHNTkyNUQ3QUNBRUMwREIxORlHpaYAAAAASUVORK5CYII=";
            var response = await _http.PostAsync(
                $"/api/log/{moduleId}/images/{result.upload}",
                new StringContent(placeholderImage, System.Text.Encoding.UTF8, "text/plain"),
                ct);
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed record ImageResult(string upload);

    public void Dispose() => _http.Dispose();

    private sealed record CreatePlanResponse([property: JsonPropertyName("id")] string Id);

    private sealed record PlanDetails(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("modules")] IReadOnlyList<TestModule>? Modules);

    private sealed record StartModuleResponse([property: JsonPropertyName("id")] string Id);

    private sealed record ModuleInfo(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("result")] string? Result);

    private sealed record BrowserState(
        [property: JsonPropertyName("urls")] IReadOnlyList<string> Urls,
        [property: JsonPropertyName("visited")] IReadOnlyList<string> Visited);
}

/// <summary>
/// A log entry from the conformance suite test log.
/// </summary>
public sealed record LogEntry(
    [property: JsonPropertyName("src")] string? Src,
    [property: JsonPropertyName("msg")] string? Msg,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("http")] string? Http,
    [property: JsonPropertyName("redirect_to")] string? RedirectTo,
    [property: JsonPropertyName("requirements")] JsonElement? Requirements,
    [property: JsonPropertyName("time")] long? Time);

/// <summary>
/// A test module within a conformance test plan.
/// </summary>
public sealed record TestModule(
    [property: JsonPropertyName("testModule")] string Name,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("result")] string? Result);

/// <summary>
/// The final result of a conformance test module run.
/// </summary>
public sealed record ModuleResult(string ModuleId, string Status, string Result)
{
    /// <summary>
    /// A module passes if the result is PASSED, WARNING, or REVIEW.
    /// WARNING indicates non-critical issues (e.g., extra claims) that don't block certification.
    /// REVIEW means the test logic passed but requires manual screenshot upload for certification.
    /// </summary>
    public bool Passed => Result is "PASSED" or "WARNING" or "REVIEW";
}

/// <summary>
/// Discriminates between the kinds of browser interaction a conformance step requires.
/// </summary>
public enum ConformanceStepKind
{
    /// <summary>An authorization endpoint URL — the browser should log in and return a code.</summary>
    Authorize,

    /// <summary>An end_session endpoint URL — the browser should confirm logout.</summary>
    EndSession,
}

/// <summary>
/// Base class for a step returned by <see cref="ConformanceSuiteClient.WaitForNextStepAsync"/>.
/// Either the module is done (<see cref="ConformanceModuleDone"/>) or a URL needs browser
/// interaction (<see cref="ConformanceUrlStep"/>).
/// </summary>

// TODO - Should we keep this?
public abstract record ConformanceStep;

/// <summary>
/// The module has reached a terminal state. No more browser interaction is needed.
/// </summary>
public sealed record ConformanceModuleDone(ModuleResult Result) : ConformanceStep;

/// <summary>
/// A URL from the conformance suite that needs browser interaction.
/// After handling, call <see cref="ConformanceSuiteClient.AcknowledgeBrowserUrlAsync"/>
/// then <see cref="ConformanceSuiteClient.WaitForNextStepAsync"/> again.
/// </summary>
public sealed record ConformanceUrlStep(
    string Url,
    string RawUrl,
    ConformanceStepKind Kind) : ConformanceStep;
