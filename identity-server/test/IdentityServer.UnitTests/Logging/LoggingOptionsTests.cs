// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Http;

namespace IdentityServer.UnitTests.Logging;

public class LoggingOptionsTests
{
    private readonly LoggingOptions options = new();

    [Fact]
    public void IgnoresOperationCanceledException()
    {
        var result = options.InvokeUnhandledExceptionLoggingFilter(
            new DefaultHttpContext() { RequestAborted = new(true) },
            new OperationCanceledException()
        );

        // False means it is suppressed
        result.ShouldBeFalse();
    }

    [Fact]
    public void OperationalCanceledExceptionIsLogged()
    {
        var result = options.InvokeUnhandledExceptionLoggingFilter(
            new DefaultHttpContext() { RequestAborted = new(false) },
            new OperationCanceledException()
        );

        result.ShouldBeTrue();
    }

    [Fact]
    public void InvokingUnhandledExceptionLoggingFilterAppliesAllFilters_ReturnsFalseIfAnyFilterReturnsFalse()
    {
        options.UnhandledExceptionLoggingFilter.ShouldNotBeNull();

        options.UnhandledExceptionLoggingFilter += (_, _) => true;
        options.UnhandledExceptionLoggingFilter += (_, _) => false;
        options.UnhandledExceptionLoggingFilter += (_, _) => true;

        var result = options.InvokeUnhandledExceptionLoggingFilter(
            new DefaultHttpContext { RequestAborted = new(true) },
            new OperationCanceledException("oops")
        );

        result.ShouldBeFalse();
    }

    [Fact]
    public void InvokingUnhandledExceptionLoggingFilterAppliesAllFilters_ReturnsTrueIfAllFilterReturnTrue()
    {
        options.UnhandledExceptionLoggingFilter.ShouldNotBeNull();

        options.UnhandledExceptionLoggingFilter += (_, _) => true;
        options.UnhandledExceptionLoggingFilter += (_, _) => true;

        var result = options.InvokeUnhandledExceptionLoggingFilter(
            new DefaultHttpContext { RequestAborted = new(false) },
            new Exception("oops")
        );

        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueIfUnhandledExceptionLoggingFilterIsNull()
    {
        options.UnhandledExceptionLoggingFilter = null!;

        var result = options.InvokeUnhandledExceptionLoggingFilter(
            new DefaultHttpContext(),
            new OperationCanceledException()
        );

        result.ShouldBeTrue();
    }
}
