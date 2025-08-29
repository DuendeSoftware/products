// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.SessionManagement.Configuration;

/// <summary>
/// This exception is thrown when a request is blocked because the trial mode session limit
/// </summary>
#pragma warning disable CA1064 // This exception is intentionally not public. It should not be catchable. 
#pragma warning disable CA1032 // I don't want explicit parameterless ctor or serialization info ctor
internal sealed class TrialModeSessionLimitExceededException : Exception
#pragma warning restore CA1032
#pragma warning restore CA1064
{
    internal TrialModeSessionLimitExceededException(string message) : base(message)
    {
    }
}
