// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.Platform.UserManagement.Licensing;

public sealed class UserCountSchedulingTests : IAsyncDisposable
{
    private const int UserLimit = 5;

    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly ControllableUserCountProvider _counter = new();
    private readonly ServiceProvider _services;
    private readonly FakeLogCollector _licenseLogs;
    private readonly UserManagementLicenseValidator _validator;

    public UserCountSchedulingTests()
    {
        var services = new ServiceCollection();
        _ = services.AddFakeLogging();
        _ = services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug));
        _ = services.AddSingleton<TimeProvider>(_time);
        LicensingModule.Register(services);
        _ = services.AddSingleton<IUserCountProvider>(_counter);

        _licenseLogs = TestUserManagementLicense.Create().WithUserLimit(UserLimit).Register(services, _time);
        _services = services.BuildServiceProvider();
        _validator = _services.GetRequiredService<UserManagementLicenseValidator>();
    }

    private FakeLogCollector ValidatorLogs => _services.GetRequiredService<FakeLogCollector>();

    // Disposing the provider disposes the validator, which cancels and drains any running check.
    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task TryStartUserCountCheck_over_limit_reports_total_against_UM_001()
    {
        _counter.Total = 1_000;

        await StartCheck();

        _counter.Calls.ShouldBe(1);
        LicenseLogsReportUserCount(1_000).ShouldBeTrue();
    }

    [Fact]
    public async Task TryStartUserCountCheck_within_interval_does_not_count_again()
    {
        await StartCheck();
        _time.Advance(TimeSpan.FromMinutes(59));

        _validator.TryStartUserCountCheck().ShouldBeNull();

        _counter.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task TryStartUserCountCheck_after_interval_counts_again()
    {
        await StartCheck();
        _time.Advance(TimeSpan.FromHours(1));

        await StartCheck();

        _counter.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task TryStartUserCountCheck_while_check_running_does_not_start_another()
    {
        var release = _counter.BlockNextCall();
        var running = _validator.TryStartUserCountCheck().ShouldNotBeNull();
        try
        {
            await _counter.WaitForCallAsync();
            _time.Advance(TimeSpan.FromHours(2));

            _validator.TryStartUserCountCheck().ShouldBeNull();
        }
        finally
        {
            _ = release.TrySetResult();
            await running.WaitAsync(TimeSpan.FromSeconds(30));
        }
        _counter.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task TryStartUserCountCheck_when_count_fails_logs_warning_and_skips_validation()
    {
        _counter.Total = 1_000;
        _counter.Failure = new InvalidOperationException("storage unavailable");

        await StartCheck();

        var warning = ValidatorLogs.GetSnapshot().Where(r => r.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.Exception.ShouldBeSameAs(_counter.Failure);
        _licenseLogs.GetSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task TryStartUserCountCheck_after_failure_retries_in_next_interval()
    {
        _counter.Total = 1_000;
        _counter.Failure = new InvalidOperationException("storage unavailable");
        await StartCheck();
        _validator.TryStartUserCountCheck().ShouldBeNull();
        _counter.Failure = null;
        _time.Advance(TimeSpan.FromHours(1));

        await StartCheck();

        _counter.Calls.ShouldBe(2);
        LicenseLogsReportUserCount(1_000).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateUserCount_returns_before_count_completes()
    {
        var release = _counter.BlockNextCall();

        _validator.ValidateUserCount();

        await _counter.WaitForCallAsync();
        ValidatorLogs.GetSnapshot().ShouldNotContain(r =>
            r.Id.Name == "UserCountCheckCompleted" || r.Id.Name == "UserCountCheckFailed");
        _ = release.TrySetResult();
        await ValidatorLogs.WaitForLogAsync(r => r.Id.Name == "UserCountCheckCompleted", TestContext.Current.CancellationToken);
        _counter.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_cancels_running_check_and_waits_for_it()
    {
        _ = _counter.BlockNextCall();
        var validatorLogs = ValidatorLogs;
        _validator.ValidateUserCount();
        await _counter.WaitForCallAsync();

        await _services.DisposeAsync();

        _counter.ObservedCancellation.ShouldBeTrue();
        validatorLogs.GetSnapshot().ShouldNotContain(r =>
            r.Id.Name == "UserCountCheckCompleted" || r.Id.Name == "UserCountCheckFailed");
        _licenseLogs.GetSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task TryStartUserCountCheck_after_dispose_does_not_start_a_check()
    {
        await _services.DisposeAsync();

        _validator.TryStartUserCountCheck().ShouldBeNull();
        _counter.Calls.ShouldBe(0);
    }

    private async Task StartCheck() =>
        await _validator.TryStartUserCountCheck().ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(30));

    private bool LicenseLogsReportUserCount(long count) =>
        _licenseLogs.GetSnapshot().Any(r => r.Level >= LogLevel.Warning && r.Message.Contains($"User Count ({count})", StringComparison.Ordinal));

    private sealed class ControllableUserCountProvider : IUserCountProvider
    {
        private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? _block;
        private int _calls;

        public long Total { get; set; }

        public Exception? Failure { get; set; }

        public int Calls => _calls;

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource BlockNextCall()
        {
            _block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _block;
        }

        public Task WaitForCallAsync() => _called.Task.WaitAsync(TimeSpan.FromSeconds(30));

        public async Task<long> GetUserCountAsync(Ct ct)
        {
            _ = Interlocked.Increment(ref _calls);
            _ = _called.TrySetResult();

            if (Interlocked.Exchange(ref _block, null) is { } block)
            {
                try
                {
                    await block.Task.WaitAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    ObservedCancellation = true;
                    throw;
                }
            }

            return Failure is { } failure ? throw failure : Total;
        }
    }
}
