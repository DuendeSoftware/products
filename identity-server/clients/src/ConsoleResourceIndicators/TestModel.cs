// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace ConsoleResourceIndicators;

internal enum TestStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

internal class TestResult
{
    public bool AccessTokenReceived { get; set; }
    public bool RefreshTokenReceived { get; set; }
    public List<RefreshResult> RefreshResults { get; set; } = [];
}

internal class RefreshResult
{
    public string Resource { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Error { get; set; }
    public bool WasExpectedError { get; set; }
}

internal class Test
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool AccessTokenExpected { get; set; } = true;
    public bool RefreshTokenExpected => Scope.Contains("offline_access") && AccessTokenExpected;
    public string Scope { get; set; } = string.Empty;
    public IEnumerable<string> Resources { get; set; } = [];
    public TestStatus Status { get; set; } = TestStatus.Pending;
    public string ErrorMessage { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TestResult Result { get; set; }
}
