// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Diagnostics.CodeAnalysis;

namespace Duende.IdentityServer.Saml.Models;

public record SamlInteractionResponse
{
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsError => ResultType == SamlInteractionResponseType.Error;

    public SamlInteractionResponseType ResultType { get; init; }

    public SamlError? Error { get; init; }

    public static SamlInteractionResponse CreateError(string statusCode, string errorDescription) => new SamlInteractionResponse()
    {
        ResultType = SamlInteractionResponseType.Error,
        Error = new SamlError
        {
            StatusCode = statusCode,
            Message = errorDescription
        }
    };

    public static SamlInteractionResponse Create(SamlInteractionResponseType type)
    {
        if (type == SamlInteractionResponseType.Error)
        {
            throw new InvalidOperationException("Cannot create error interaction response without error details");
        }

        return new SamlInteractionResponse()
        {
            ResultType = type
        };
    }
}
