// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ConformanceReport;

[Flags]
internal enum ConformanceReportDPoPValidationMode
{
    None = 0,
    Nonce = 1,
    Iat = 2
}
