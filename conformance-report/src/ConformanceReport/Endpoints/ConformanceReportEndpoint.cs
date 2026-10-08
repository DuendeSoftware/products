// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.ConformanceReport.Licensing;
using Duende.ConformanceReport.Services;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Endpoints;

/// <summary>
/// Endpoint for generating conformance assessment reports.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConformanceReportEndpoint"/> class.
/// </remarks>
internal sealed partial class ConformanceReportEndpoint(
    ConformanceReportAssessmentService assessmentService,
    ConformanceReportLicenseValidator licenseValidator,
    IOptions<ConformanceReportOptions> options,
    ILogger<ConformanceReportEndpoint> logger)
{
    private readonly ConformanceReportOptions _options = options.Value;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing conformance HTML report request")]
    private partial void LogProcessingRequest();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conformance endpoint accessed but feature is not enabled")]
    private partial void LogFeatureNotEnabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error generating conformance HTML report")]
    private partial void LogReportGenerationError(Exception ex);

    /// <summary>
    /// Processes requests for the HTML conformance report.
    /// </summary>
    public async Task<IResult> GetHtmlReportAsync(Ct ct)
    {
        LogProcessingRequest();

        if (!_options.Enabled)
        {
            LogFeatureNotEnabled();
            return Results.NotFound();
        }

        if (!licenseValidator.ValidateConformanceReport())
        {
            return Results.NotFound();
        }

        try
        {
            var report = await assessmentService.GenerateReportAsync(ct);

            using var slice = Internal.Slices.ConformanceReport.Create(report);
            var sb = new StringBuilder();
            await using var writer = new StringWriter(sb);
            await slice.RenderAsync(writer, cancellationToken: ct);

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
