// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class StubHandleGenerationService : DefaultHandleGenerationService, IHandleGenerationService
{
    public string Handle { get; set; }

    public new Task<string> GenerateAsync(CT ct, int length = 32)
    {
        if (Handle != null)
        {
            return Task.FromResult(Handle);
        }

        return base.GenerateAsync(ct, length);
    }
}
