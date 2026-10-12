// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.ConformanceReport.Internal.Models;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Service for assessing configuration conformance against OAuth 2.1 and FAPI 2.0 profiles.
/// </summary>
internal class ConformanceReportAssessmentService
{
    private readonly IConformanceReportServerOptionsProvider _serverOptionsProvider;
    private readonly ConformanceReportOptions _conformanceOptions;
    private readonly IConformanceReportClientStore _clientStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ConformanceReportLicenseInfo? _licenseInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConformanceReportAssessmentService"/> class.
    /// </summary>
    /// <param name="conformanceOptions">The conformance report options.</param>
    /// <param name="serverOptionsProvider">Supplies the server options to assess. Called once per report.</param>
    /// <param name="clientStore">The store that supplies the clients to assess.</param>
    /// <param name="httpContextAccessor">The accessor used to build the report URL.</param>
    /// <param name="licenseInfo">Optional license information to include in the report.</param>
    public ConformanceReportAssessmentService(
        IOptions<ConformanceReportOptions> conformanceOptions,
        IConformanceReportServerOptionsProvider serverOptionsProvider,
        IConformanceReportClientStore clientStore,
        IHttpContextAccessor httpContextAccessor,
        ConformanceReportLicenseInfo? licenseInfo = null)
    {
        _conformanceOptions = conformanceOptions.Value;
        _serverOptionsProvider = serverOptionsProvider;
        _clientStore = clientStore;
        _httpContextAccessor = httpContextAccessor;
        _licenseInfo = licenseInfo;
    }

    /// <summary>
    /// Generates a complete conformance assessment report.
    /// </summary>
    /// <remarks>
    /// Server options are fetched from the <see cref="IConformanceReportServerOptionsProvider"/>
    /// on each call, so the report reflects the current configuration.
    /// </remarks>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A conformance report containing the assessment results.</returns>
    public async Task<ConformanceReportResult> GenerateReportAsync(Ct ct)
    {
        var serverOptions = await _serverOptionsProvider.GetServerOptionsAsync(ct);
        var clients = await _clientStore.GetAllClientsAsync(ct);
        var clientList = clients.ToList();

        ProfileResult? oauth21Result = null;
        ProfileResult? fapi2Result = null;

        if (_conformanceOptions.EnableOAuth21Assessment)
        {
            oauth21Result = AssessOAuth21Profile(new OAuth21Assessor(serverOptions), clientList);
        }

        if (_conformanceOptions.EnableFapi2SecurityAssessment)
        {
            fapi2Result = AssessFapi2SecurityProfile(new Fapi2SecurityAssessor(serverOptions), clientList);
        }

        var overallStatus = DetermineOverallStatus(oauth21Result, fapi2Result);
        var reportUrl = BuildReportUrl();

        return new ConformanceReportResult
        {
            Version = GetVersion(),
            License = _licenseInfo,
            Url = reportUrl,
            Status = overallStatus,
            AssessedAt = DateTimeOffset.UtcNow,
            Profiles = new ConformanceReportProfiles
            {
                OAuth21 = oauth21Result,
                Fapi2Security = fapi2Result
            },
            OverallSummary = BuildOverallSummary(clientList.Count, oauth21Result, fapi2Result),
            HostCompanyName = _conformanceOptions.HostCompanyName,
            HostCompanyLogoUrl = _conformanceOptions.HostCompanyLogoUrl
        };
    }

    private static ClientResult BuildClientResult(ConformanceReportClient client, IReadOnlyList<Finding> findings) =>
        new()
        {
            ClientId = client.ClientId,
            ClientName = client.ClientName,
            Status = DetermineStatusFromFindings(findings),
            Findings = findings
        };

    private static ProfileResult AssessOAuth21Profile(OAuth21Assessor assessor, List<ConformanceReportClient> clients)
    {
        var serverFindings = assessor.AssessServer(clients);
        var clientResults = clients.Select(c => BuildClientResult(c, assessor.AssessClient(c))).ToList();

        var serverStatus = DetermineStatusFromFindings(serverFindings);
        var overallStatus = DetermineProfileStatus(serverStatus, clientResults);

        return new ProfileResult
        {
            Name = "OAuth 2.1",
            SpecVersion = "draft-16",
            SpecStatus = "draft",
            Note = "OAuth 2.1 is currently a draft specification. Assessment rules may change as the specification evolves.",
            Status = overallStatus,
            Server = new ServerResult
            {
                Status = serverStatus,
                Findings = serverFindings
            },
            Clients = clientResults,
            Summary = BuildProfileSummary(clientResults)
        };
    }

    private static ProfileResult AssessFapi2SecurityProfile(Fapi2SecurityAssessor assessor, List<ConformanceReportClient> clients)
    {
        var serverFindings = assessor.AssessServer();
        var clientResults = clients.Select(c => BuildClientResult(c, assessor.AssessClient(c))).ToList();

        var serverStatus = DetermineStatusFromFindings(serverFindings);
        var overallStatus = DetermineProfileStatus(serverStatus, clientResults);

        return new ProfileResult
        {
            Name = "FAPI 2.0 Security Profile",
            SpecVersion = "1.0",
            SpecStatus = "final",
            Status = overallStatus,
            Server = new ServerResult
            {
                Status = serverStatus,
                Findings = serverFindings
            },
            Clients = clientResults,
            Summary = BuildProfileSummary(clientResults)
        };
    }

    private static ConformanceReportStatus DetermineStatusFromFindings(IReadOnlyList<Finding> findings)
    {
        if (findings.Any(f => f.Status == FindingStatus.Fail || f.Status == FindingStatus.Error))
        {
            return ConformanceReportStatus.Fail;
        }

        if (findings.Any(f => f.Status == FindingStatus.Warning))
        {
            return ConformanceReportStatus.Warn;
        }

        return ConformanceReportStatus.Pass;
    }

    private static ConformanceReportStatus DetermineProfileStatus(ConformanceReportStatus serverStatus, List<ClientResult> clientResults)
    {
        if (serverStatus == ConformanceReportStatus.Fail || clientResults.Any(c => c.Status == ConformanceReportStatus.Fail))
        {
            return ConformanceReportStatus.Fail;
        }

        if (serverStatus == ConformanceReportStatus.Warn || clientResults.Any(c => c.Status == ConformanceReportStatus.Warn))
        {
            return ConformanceReportStatus.Warn;
        }

        return ConformanceReportStatus.Pass;
    }

    private static ConformanceReportStatus DetermineOverallStatus(ProfileResult? oauth21, ProfileResult? fapi2)
    {
        var statuses = new List<ConformanceReportStatus>();

        if (oauth21 is not null)
        {
            statuses.Add(oauth21.Status);
        }

        if (fapi2 is not null)
        {
            statuses.Add(fapi2.Status);
        }

        if (statuses.Count == 0)
        {
            return ConformanceReportStatus.Pass;
        }

        if (statuses.Any(s => s == ConformanceReportStatus.Fail))
        {
            return ConformanceReportStatus.Fail;
        }

        if (statuses.Any(s => s == ConformanceReportStatus.Warn))
        {
            return ConformanceReportStatus.Warn;
        }

        return ConformanceReportStatus.Pass;
    }

    private static ProfileSummary BuildProfileSummary(List<ClientResult> clientResults) =>
        new()
        {
            TotalClients = clientResults.Count,
            PassingClients = clientResults.Count(c => c.Status == ConformanceReportStatus.Pass),
            WarningClients = clientResults.Count(c => c.Status == ConformanceReportStatus.Warn),
            FailingClients = clientResults.Count(c => c.Status == ConformanceReportStatus.Fail)
        };

    private static OverallSummary BuildOverallSummary(int totalClients, ProfileResult? oauth21, ProfileResult? fapi2) =>
        new()
        {
            TotalClients = totalClients,
            OAuth21 = oauth21 is not null
                ? new ProfileStatusSummary
                {
                    Passing = oauth21.Summary.PassingClients,
                    Warning = oauth21.Summary.WarningClients,
                    Failing = oauth21.Summary.FailingClients
                }
                : new ProfileStatusSummary(),
            Fapi2Security = fapi2 is not null
                ? new ProfileStatusSummary
                {
                    Passing = fapi2.Summary.PassingClients,
                    Warning = fapi2.Summary.WarningClients,
                    Failing = fapi2.Summary.FailingClients
                }
                : new ProfileStatusSummary()
        };

    private Uri BuildReportUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return new Uri("about:blank");
        }

        var pathPrefix = _conformanceOptions.PathPrefix.Trim('/');
        return new Uri($"{request.Scheme}://{request.Host}/{pathPrefix}/{ConformanceReportConstants.FeaturePath}");
    }

    private static string GetVersion()
    {
        var assembly = typeof(ConformanceReportResult).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        // Strip git hash if present (MinVer adds +hash)
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        return plusIndex > 0 ? version[..plusIndex] : version;
    }
}
