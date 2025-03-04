using Duende.IdentityServer.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Logging;

public class SanitizedLoggerTests
{
    private readonly FakeLogger<SanitizedLoggerTests> _fakeLogger;

    private readonly SanitizedLogger<SanitizedLoggerTests> _subject;

    public SanitizedLoggerTests()
    {
        _fakeLogger = new FakeLogger<SanitizedLoggerTests>();
        _subject = new SanitizedLogger<SanitizedLoggerTests>(_fakeLogger);
    }
    
    [Fact]
    public void LogTrace_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Trace, false);
        
        _subject.LogTrace("This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogTrace_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Trace, true);
        
        _subject.LogTrace("This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }

    [Fact]
    public void LogDebug_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Debug, false);
        
        _subject.LogDebug("This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogDebug_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Debug, true);
        
        _subject.LogDebug("This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }
    
    [Fact]
    public void LogInformation_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Information, false);
        
        _subject.LogInformation("This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogInformation_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Information, true);
        
        _subject.LogInformation("This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }
    
    [Fact]
    public void LogWarning_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Warning, false);
        
        _subject.LogWarning("This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogWarning_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Warning, true);
        
        _subject.LogWarning("This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }
    
    [Fact]
    public void LogError_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Error, false);
        
        _subject.LogError("This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogError_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Error, true);
        
        _subject.LogError("This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }
    
    [Fact]
    public void LogCritical_does_not_log_message_when_debug_not_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Critical, false);
        
        _subject.LogCritical(new Exception(),"This should not log anything {input}", "nope");
        
        _fakeLogger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public void LogCritical_logs_sanitized_message_when_debug_enabled()
    {
        _fakeLogger.ControlLevel(LogLevel.Critical, true);
        
        _subject.LogCritical(new Exception(),"This should not have newlines {input}", $"testing{Environment.NewLine} newlines");

        _fakeLogger.LatestRecord.Message.ShouldBe("This should not have newlines testing newlines");
    }
}