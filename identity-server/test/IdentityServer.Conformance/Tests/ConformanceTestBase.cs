// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// Base class for conformance test classes. Provides helper methods for driving
/// browser interaction steps returned by <see cref="ConformanceSuiteClient.WaitForNextStepAsync"/>.
///
/// Each [Fact] test method describes its own interaction flow explicitly:
/// <code>
///   var moduleId = await StartModuleAsync("oidcc-rp-initiated-logout");
///   await Login(await NextStep(moduleId));
///   await Logout(await NextStep(moduleId));
///   await AssertPassedAsync(moduleId);
/// </code>
/// </summary>
public abstract class ConformanceTestBase
{
    protected abstract ConformanceSuiteFixture Fixture { get; }
    protected abstract ITestOutputHelper Output { get; }
    protected abstract string CallbackUrlPrefix { get; }
    protected abstract string InternalHost { get; }
    protected abstract string ExternalHost { get; }

    /// <summary>
    /// Maps module IDs to their human-readable names for use in file naming.
    /// </summary>
    private readonly Dictionary<string, string> _moduleNames = new();

    /// <summary>
    /// Tracks when each module started, for filtering container logs.
    /// </summary>
    private readonly Dictionary<string, DateTime> _moduleStartTimes = new();

    /// <summary>
    /// HttpClient for calling the log marker endpoint on the IdP.
    /// </summary>
    private static readonly HttpClient MarkerClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    /// <summary>
    /// Starts a test module and returns its ID.
    /// Throws a dynamic skip if the module is not in the plan.
    /// </summary>
    protected async Task<string> StartModuleAsync(string moduleName)
    {
        Output.WriteLine($"Starting module: {moduleName}");

        if (!Fixture.Modules.Any(m => m.Name == moduleName))
        {
            throw new InvalidOperationException(
                $"{Xunit.v3.DynamicSkipToken.Value}Module '{moduleName}' is not in the test plan " +
                $"(available: {string.Join(", ", Fixture.Modules.Select(m => m.Name))})");
        }

        var moduleId = await Fixture.Client.StartTestModuleAsync(Fixture.PlanId, moduleName);
        _moduleNames[moduleId] = moduleName;
        _moduleStartTimes[moduleId] = DateTime.UtcNow;
        await WriteLogMarkerAsync($"START {moduleName} ({moduleId})");
        Output.WriteLine($"Module started with ID: {moduleId}");
        return moduleId;
    }

    /// <summary>
    /// Polls the conformance suite until the next interaction step is ready or the module finishes.
    /// </summary>
    protected Task<ConformanceStep> NextStep(
        string moduleId) =>
        Fixture.Client.WaitForNextStepAsync(
            moduleId,
            internalHost: InternalHost,
            externalHost: ExternalHost,
            log: msg => Output.WriteLine(msg));



    /// <summary>
    /// Navigates to the authorize endpoint URL, logging in if needed. The authorization
    /// may succeed (redirecting to the callback), fail (returning an error to the callback),
    /// or skip login entirely (e.g., prompt=none with an existing session). Asserts that
    /// the step is an Authorize step, acknowledges the browser URL, and returns the step.
    /// </summary>
    protected async Task<ConformanceStep> Authorize(string moduleId, ConformanceStep step)
    {
        var s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.Authorize,
            $"Expected an Authorize step but got {step}");

        Output.WriteLine($"Login: {s.Url}");
        var result = await Fixture.LoginAutomation.CompleteAuthorizationAsync(
            s.Url, CallbackUrlPrefix, log: msg => Output.WriteLine(msg));
        result.Succeeded.ShouldBeTrue($"Login did not reach callback. Final URL: {result.FinalUrl}");

        await Fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);
        return step;
    }

    /// <summary>
    /// Asserts the step is an Authorize step, denies the authorization request,
    /// acknowledges the URL, and returns the step.
    /// </summary>
    protected async Task<ConformanceStep> DenyAuthorization(string moduleId, ConformanceStep step)
    {
        var s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.Authorize,
            $"Expected an Authorize step but got {step}");

        Output.WriteLine($"DenyLogin: {s.Url}");
        await Fixture.LoginAutomation.DenyAuthorizationAsync(
            s.Url, CallbackUrlPrefix, log: msg => Output.WriteLine(msg));

        await Fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);
        return step;
    }

    /// <summary>
    /// Asserts the step is an EndSession step, drives the logout flow through Playwright,
    /// acknowledges the URL, and returns the step.
    /// </summary>
    protected async Task<AuthorizationResult> EndSession(string moduleId, ConformanceStep step)
    {
        var s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.EndSession,
            $"Expected an EndSession step but got {step}");

        Output.WriteLine($"Logout: {s.Url}");

        // Drive the logout through Playwright so the session cookie is cleared in
        // the same browser context that holds it. CompleteAuthorizationAsync handles
        // the logout confirmation page and the post-logout redirect link (if present).
        // If there is no post-logout redirect, it lands on the LoggedOut page and
        // returns ErrorPage — that is expected and not a failure for logout tests.
        var result = await Fixture.LoginAutomation.CompleteAuthorizationAsync(
            s.Url, CallbackUrlPrefix, log: msg => Output.WriteLine(msg));

        await Fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);
        return result;
    }

    /// <summary>
    /// Acknowledges a URL step without navigating to it.
    /// Use this when the authorization flow was already completed and the URL
    /// is just a re-check that should not trigger a new flow.
    /// </summary>
    protected async Task<ConformanceStep> AcknowledgeBrowserUrl(string moduleId, ConformanceStep step)
    {
        var s = step.ShouldBeOfType<ConformanceUrlStep>(
            $"Expected a URL step but got {step}");

        Output.WriteLine($"Acknowledging browser URL (no navigation): {s.Url}");
        await Fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);
        return step;
    }

    /// <summary>
    /// For error-page tests: navigates to the authorize URL, asserts that the browser
    /// landed on an error page (not the callback), acknowledges the browser URL, uploads
    /// a placeholder screenshot to satisfy the suite's review requirement, and waits for
    /// the module to reach FINISHED with a REVIEW result.
    /// </summary>
    // TODO - Rename this so that the tests read more obviously
    protected async Task AssertErrorPageAsync(string moduleId, ConformanceStep step)
    {
        var s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.Authorize,
            $"Expected an Authorize step but got {step}");

        Output.WriteLine($"AssertErrorPageAsync: navigating to {s.Url}");
        var result = await Fixture.LoginAutomation.CompleteAuthorizationAsync(
            s.Url, CallbackUrlPrefix, log: msg => Output.WriteLine(msg));

        result.Succeeded.ShouldBeFalse(
            $"Expected an error page but the browser reached the callback at: {result.FinalUrl}");
        Output.WriteLine($"Confirmed error page at: {result.FinalUrl}");

        await Fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);

        // Upload a placeholder screenshot so the suite's waitForPlaceholders background
        // thread detects all placeholders are filled and transitions the module to FINISHED.
        Output.WriteLine("Uploading placeholder screenshot...");
        await Fixture.Client.UploadPlaceholderScreenshotAsync(moduleId);

        // Wait for the module to finish (should happen within ~30s as the suite polls)
        ModuleResult? moduleResult = null;
        try
        {
            moduleResult = await Fixture.Client.WaitForDoneAsync(
                moduleId,
                log: msg => Output.WriteLine(msg));

            Output.WriteLine($"Module completed: status={moduleResult.Status}, result={moduleResult.Result}");
            moduleResult.Result.ShouldBe("REVIEW",
                $"Expected REVIEW result for error-page test but got '{moduleResult.Result}'");
        }
        finally
        {
            await CaptureModuleDetailsAsync(moduleId, moduleResult);
        }
    }

    /// <summary>
    /// Common flow for tests that just need a single authorize request.
    /// Starts the module, waits for the authorize URL, navigates it, and asserts passed.
    /// Handles modules that finish immediately (no browser interaction needed).
    /// </summary>
    protected async Task SimpleLogin(string moduleName)
    {
        await Fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync(moduleName);
        try
        {
            var step = await NextStep(moduleId);
            if (step is ConformanceModuleDone)
            {
                await AssertPassedAsync(moduleId, step);
                return;
            }

            await Authorize(moduleId, step);
            await AssertPassedAsync(moduleId, step);
        }
        catch
        {
            await CaptureModuleDetailsAsync(moduleId);
            throw;
        }
    }

    /// <summary>
    /// Common flow for logout tests: authorize (possibly multiple times) → endsession → assert.
    /// When <paramref name="needsScreenshot"/> is true, asserts the browser stayed on an IS page
    /// (not redirected to post_logout_redirect_uri) and uploads a placeholder screenshot.
    /// After logout, handles remaining browser URLs (acknowledges re-checks, navigates prompt=none).
    /// </summary>
    protected async Task SimpleLogout(string moduleName, bool needsScreenshot = false)
    {
        await Fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync(moduleName);
        try
        {
            // Handle all authorize steps until we get an EndSession step
            var step = await NextStep(moduleId);
            await Authorize(moduleId, step);

            // Now we should have an EndSession step (or module done)
            step = await NextStep(moduleId);
            if (step is ConformanceModuleDone)
            {
                await AssertPassedAsync(moduleId, step);
                return;
            }

            var endSessionResult = await EndSession(moduleId, step);

            if (needsScreenshot)
            {
                // These tests have bad/missing parameters — IS should NOT redirect to the
                // post_logout_redirect_uri. The browser should stay on an IS page.
                endSessionResult.Succeeded.ShouldBeFalse(
                    $"Expected to stay on IS logout page but was redirected to: {endSessionResult.FinalUrl}");
                Output.WriteLine($"Confirmed: browser stayed on IS page at {endSessionResult.FinalUrl}");
                await Fixture.Client.UploadPlaceholderScreenshotAsync(moduleId);
            }

            await AssertPassedAsync(moduleId, step);
        }
        catch
        {
            await CaptureModuleDetailsAsync(moduleId);
            throw;
        }
    }

    /// <summary>
    /// Polls until the module is done, then asserts it passed.
    /// Call this after all interaction steps are complete.
    /// </summary>
    protected Task AssertPassedAsync(string moduleId, ConformanceStep? lastStep = null) =>
        AssertPassedAsync(moduleId, lastStep!, navigateBrowserUrls: false);

    /// <summary>
    /// Polls until the module is done, then asserts it passed.
    /// Uses a custom <paramref name="browserUrlHandler"/> for pending browser URLs.
    /// </summary>
    protected Task AssertPassedAsync(string moduleId, ConformanceStep lastStep, Func<string, Task> browserUrlHandler) =>
        AssertPassedAsync(moduleId, lastStep, navigateBrowserUrls: true, customBrowserUrlHandler: browserUrlHandler);

    /// <summary>
    /// Polls until the module is done, then asserts it passed.
    /// When <paramref name="navigateBrowserUrls"/> is false, pending browser-API URLs are
    /// ignored and the module is left to finish on its own (use this when navigating would
    /// re-trigger consent flows).
    /// </summary>
    protected async Task AssertPassedAsync(string moduleId, ConformanceStep lastStep, bool navigateBrowserUrls,
        Func<string, Task>? customBrowserUrlHandler = null)
    {
        ModuleResult? result = null;
        try
        {
            if (lastStep is ConformanceModuleDone done)
            {
                result = done.Result;
            }
            else
            {
                var browserUrlHandler = customBrowserUrlHandler;
                if (browserUrlHandler is null && navigateBrowserUrls)
                {
                    browserUrlHandler = async url =>
                    {
                        Output.WriteLine($"AssertPassedAsync: navigating to browser URL: {url}");
                        await Fixture.LoginAutomation.CompleteAuthorizationAsync(
                            url, CallbackUrlPrefix, log: msg => Output.WriteLine(msg));
                    };
                }

                result = await Fixture.Client.WaitForDoneAsync(
                    moduleId,
                    internalHost: InternalHost,
                    externalHost: ExternalHost,
                    browserUrlHandler: browserUrlHandler,
                    log: msg => Output.WriteLine(msg));
            }

            Output.WriteLine($"Module completed: status={result.Status}, result={result.Result}");

            // Final safety check: query the conformance suite API directly to confirm
            // the module's actual result. This guards against false positives where our
            // polling/recovery logic derives a passing result but the module actually failed.
            var finalResult = await Fixture.Client.GetModuleResultAsync(moduleId);
            if (finalResult != null && finalResult.Result != result.Result)
            {
                Output.WriteLine($"WARNING: Derived result '{result.Result}' differs from API result '{finalResult.Result}'. Using API result.");
                result = finalResult;
            }

            if (result.Result == "SKIPPED")
            {
                throw new InvalidOperationException(
                    $"{Xunit.v3.DynamicSkipToken.Value}Module was SKIPPED (optional feature not implemented)");
            }

            if (!result.Passed)
            {
                throw new ShouldAssertException(
                    $"Module did not pass: status={result.Status}, result={result.Result}");
            }
        }
        finally
        {
            await CaptureModuleDetailsAsync(moduleId, result);
        }
    }

    protected async Task CaptureModuleDetailsAsync(string moduleId, ModuleResult? result = null)
    {
        var moduleName = _moduleNames.GetValueOrDefault(moduleId, moduleId);
        string? reportDir = null;

        // Export HTML report
        try
        {
            var exportPath = await Fixture.Client.ExportTestHtmlAsync(
                moduleId, Fixture.PlanName, moduleName);
            if (exportPath != null)
            {
                reportDir = Path.GetDirectoryName(exportPath);
                Output.WriteLine($"Report: {exportPath}");
            }
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to export HTML report: {ex.Message}");
        }

        // Ensure report directory exists for log files
        reportDir ??= Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "conformance-reports", Fixture.PlanName, moduleName));
        Directory.CreateDirectory(reportDir);

        // Save conformance suite logs to file
        try
        {
            var logs = await Fixture.Client.GetTestLogAsync(moduleId);
            var logLines = new List<string>();
            foreach (var entry in logs)
            {
                logLines.Add($"[{entry.Result ?? "INFO"}] {entry.Src}: {entry.Msg}");
                if (entry.Requirements is not null)
                {
                    logLines.Add($"  Requirements: {entry.Requirements}");
                }

                // Only show failures/warnings in test output
                if (entry.Result is "FAILURE" or "WARNING" or "REVIEW")
                {
                    Output.WriteLine($"[{entry.Result}] {entry.Src}: {entry.Msg}");
                }
            }

            await File.WriteAllLinesAsync(Path.Combine(reportDir, "conformance-log.txt"), logLines);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to retrieve conformance logs: {ex.Message}");
        }

        // Save container logs to file, extracted between log markers
        try
        {
            var endMarker = $"END {moduleName} ({moduleId})";
            await WriteLogMarkerAsync(endMarker);

            var startMarker = $"===== START {moduleName} ({moduleId}) =====";
            var endMarkerFull = $"===== {endMarker} =====";

            var since = _moduleStartTimes.GetValueOrDefault(moduleId);
            var containerLogs = await Fixture.GetServiceLogsAsync(ConformanceSuiteFixture.IdpServiceName, since: since);

            // Extract only the lines between our markers
            var lines = containerLogs.Split('\n');
            var capturing = false;
            var captured = new List<string>();
            foreach (var line in lines)
            {
                if (line.Contains(startMarker))
                {
                    capturing = true;
                    continue;
                }

                if (line.Contains(endMarkerFull))
                {
                    break;
                }

                if (capturing)
                {
                    captured.Add(line);
                }
            }

            var resultText = captured.Count > 0 ? string.Join('\n', captured) : containerLogs;
            await File.WriteAllTextAsync(Path.Combine(reportDir, "container-log.txt"), resultText);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Failed to retrieve container logs: {ex.Message}");
        }
    }

    private async Task WriteLogMarkerAsync(string message)
    {
        try
        {
            await MarkerClient.GetAsync($"https://{ExternalHost}/conformance/log-marker?message={Uri.EscapeDataString(message)}");
        }
        catch
        {
            // Best-effort — don't fail the test if the marker can't be written
        }
    }
}
