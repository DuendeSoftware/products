// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Licensing.Enforcement;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{FeatureName} is being used but no Duende license is configured. Please start a conversation with us: https://duende.link/l/contact")]
    internal static partial void FeatureUsedNoLicense(this ILogger logger, string featureName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{FeatureName} is being used but is not included in your Duende license. Please start a conversation with us: https://duende.link/l/contact")]
    internal static partial void FeatureNotLicensed(this ILogger logger, string featureName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{FeatureName} ({Actual}) exceeds licensed limit ({Limit}) but is within grace threshold ({Grace}). Please start a conversation with us: https://duende.link/l/contact")]
    internal static partial void QuantizedExceedsLimit(this ILogger logger, string featureName, int actual, int limit, int grace);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{FeatureName} ({Actual}) exceeds licensed grace threshold ({Grace}). Please start a conversation with us: https://duende.link/l/contact")]
    internal static partial void QuantizedExceedsGrace(this ILogger logger, string featureName, int actual, int grace);
}
