// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

internal interface IConformanceReportClientStore
{
    Task<IEnumerable<ConformanceReportClient>> GetAllClientsAsync(CT ct);
}
