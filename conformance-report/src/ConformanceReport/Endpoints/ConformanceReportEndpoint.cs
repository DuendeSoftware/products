// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.ConformanceReport.Services;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Endpoints;

/// <summary>
/// Endpoint for generating conformance assessment reports.
/// </summary>
internal sealed partial class ConformanceReportEndpoint
{
    private readonly ConformanceReportAssessmentService _assessmentService;
    private readonly ConformanceReportOptions _options;
    private readonly ILogger<ConformanceReportEndpoint> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConformanceReportEndpoint"/> class.
    /// </summary>
    public ConformanceReportEndpoint(
        ConformanceReportAssessmentService assessmentService,
        IOptions<ConformanceReportOptions> options,
        ILogger<ConformanceReportEndpoint> logger)
    {
        _assessmentService = assessmentService;
        _options = options.Value;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing conformance HTML report request")]
    private partial void LogProcessingRequest();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conformance endpoint accessed but feature is not enabled")]
    private partial void LogFeatureNotEnabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error generating conformance HTML report")]
    private partial void LogReportGenerationError(Exception ex);

    /// <summary>
    /// Processes requests for the HTML conformance report.
    /// </summary>
    public async Task<IResult> GetHtmlReportAsync(HttpContext context, Ct ct)
    {
        LogProcessingRequest();

        if (!_options.Enabled)
        {
            LogFeatureNotEnabled();
            return Results.NotFound();
        }

        try
        {
            var report = await _assessmentService.GenerateReportAsync(ct);

            using var slice = Duende.ConformanceReport.Slices.ConformanceReport.Create(report);
            var sb = new StringBuilder();
            await using var writer = new System.IO.StringWriter(sb);
#pragma warning disable CA2016 // RenderAsync overload for TextWriter doesn't accept CancellationToken
            await slice.RenderAsync(writer);
#pragma warning restore CA2016

            return Results.Content(sb.ToString(), "text/html");
        }
        catch (InvalidOperationException ex)
        {
            LogReportGenerationError(ex);
            return Results.Problem(
                title: "Error generating conformance report",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
