// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class Extensions
{
    internal static SubjectIdDso.V1 ToDso(this ISubjectId vo) => vo switch
    {
        EmailAddress id => new SubjectIdDso.V1(SubjectIdDso.V1.TypeEmail, id.Value),
        OpaqueSubjectId id => new SubjectIdDso.V1(SubjectIdDso.V1.TypeOpaque, id.Value),
        PhoneNumber id => new SubjectIdDso.V1(SubjectIdDso.V1.TypePhoneNumber, id.Value),
        _ => throw new ArgumentException($"Invalid {nameof(ISubjectId)} type", nameof(vo))
    };

    internal static ISubjectId ToValueObject(this SubjectIdDso.V1 dso) => dso.Type switch
    {
        SubjectIdDso.V1.TypeEmail => EmailAddress.Load(dso.Value),
        SubjectIdDso.V1.TypeOpaque => OpaqueSubjectId.Load(dso.Value),
        SubjectIdDso.V1.TypePhoneNumber => PhoneNumber.Load(dso.Value),
        _ => throw new ArgumentException($"Invalid {nameof(SubjectIdDso.V1.Type)}", nameof(dso))
    };

    internal static Pbkdf2HashedPassword ToValueObject(this Pbkdf2HashedPasswordDso.V1 dso) =>
        Pbkdf2HashedPassword.Load(
            Pbkdf2Inputs.Load(
                Pbkdf2Salt.Load(Convert.FromBase64String(dso.Salt)),
                Pbkdf2PseudorandomFunctionName.Load(dso.PseudorandomFunction),
                Pbkdf2IterationCount.Load(dso.IterationCount),
                Pbkdf2HashFunctionDigestSize.Load(dso.HashFunctionDigestSize)),
            Pbkdf2MasterKey.Load(Convert.FromBase64String(dso.MasterKey)));

    internal static Pbkdf2HashedPasswordDso.V1 ToDso(this Pbkdf2HashedPassword vo) => new(
        Convert.ToBase64String(vo.Inputs.Salt.Bytes.ToArray()),
        vo.Inputs.PseudorandomFunctionName.Value,
        vo.Inputs.IterationCount.Number,
        vo.Inputs.HashFunctionDigestSize.Number,
        Convert.ToBase64String(vo.MasterKey.Bytes.ToArray()));

    internal static HashedPassword ToValueObject(this HashedPasswordDso.V1 dso) =>
        HashedPassword.Load(new HashedPasswordData(
            dso.AlgorithmId,
            Convert.FromBase64String(dso.Hash),
            Convert.FromBase64String(dso.Salt),
            dso.Parameters ?? new Dictionary<string, string>()));

    internal static HashedPasswordDso.V1 ToDso(this HashedPassword vo) => new(
        vo.Data.AlgorithmId,
        Convert.ToBase64String([.. vo.Data.Hash]),
        Convert.ToBase64String([.. vo.Data.Salt]),
        vo.Data.Parameters.Count > 0
            ? new Dictionary<string, string>(vo.Data.Parameters)
            : null);
}
