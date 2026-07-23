// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Otp.Internal.Storage;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class OtpWorkflowRepository(IStoreFactory storeFactory)
{
    internal enum Keys
    {
        Address = 1,
        Token = 2
    }

    internal async Task<CreateResult> CreateAsync(OtpWorkflow workflow, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.CreateAsync(
            workflow.Id.Uuid,
            ToDso(workflow),
            [
                DataStorageKey.Create(OtpWorkflowAddressDskV1.Create(workflow.Address)),
                .. workflow.Token != null ? [DataStorageKey.Create(OtpWorkflowTokenDskV1.Create(workflow.Token))] : Array.Empty<DataStorageKey>()
            ],
            [],
            Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<UpdateResult> UpdateAsync(OtpWorkflow workflow, int expectedVersion, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        return await store.UpdateAsync(
            workflow.Id.Uuid,
            ToDso(workflow),
            expectedVersion,
            [
                DataStorageKey.Create(OtpWorkflowAddressDskV1.Create(workflow.Address)),
                .. workflow.Token != null ? [DataStorageKey.Create(OtpWorkflowTokenDskV1.Create(workflow.Token))] : Array.Empty<DataStorageKey>()
            ],
            [],
            expiration: Expiration.NoExpiration,
            [],
            ct);
    }

    internal async Task<(OtpWorkflow OtpWorkflow, int Version)?> TryReadAsync(OtpAddress address, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(OtpWorkflowDso.EntityType, DataStorageKey.Create(OtpWorkflowAddressDskV1.Create(address)), ct);
        return result.Found
            ? (ToEntity(result.Dso), result.Version.Value)
            : null;
    }

    internal async Task<(OtpWorkflow OtpWorkflow, int Version)?> TryReadAsync(OtpToken token, Ct ct)
    {
        var store = await storeFactory.GetStore(ct);
        var result = await store.TryReadAsync(OtpWorkflowDso.EntityType, DataStorageKey.Create(OtpWorkflowTokenDskV1.Create(token)), ct);
        return result.Found
            ? (ToEntity(result.Dso), result.Version.Value)
            : null;
    }

    private static OtpWorkflowDso.V1 ToDso(OtpWorkflow workflow) => new(
        workflow.Id.Uuid.Value,
        new OtpAddressDso.V1(workflow.Address.Channel.Value, workflow.Address.SubjectId.ToDso()),
        workflow.HashedOtp?.ToDso(),
        workflow.Token?.Value,
        workflow.OtpExpiresAt,
        workflow.OtpCreationBlockedUntil,
        workflow.Attempts.ToList());

    private static OtpWorkflow ToEntity(IDataStorageObject value) =>
        value switch
        {
            OtpWorkflowDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    private static OtpWorkflow ToEntity(OtpWorkflowDso.V1 dso) => OtpWorkflow.Load(
        OtpWorkflowId.Load(dso.Id),
        OtpAddress.Load(OtpChannel.Load(dso.Address.Channel), dso.Address.SubjectId.ToValueObject()),
        dso.HashedOtp?.ToValueObject(),
        dso.Token.HasValue ? OtpToken.Load(dso.Token.Value) : null,
        dso.OtpExpiresAt,
        dso.OtpCreationBlockedUntil,
        dso.Attempts);
}
