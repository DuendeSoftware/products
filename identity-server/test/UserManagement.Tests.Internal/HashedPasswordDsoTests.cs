// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;

namespace Duende.Platform.UserManagement;

public sealed class HashedPasswordDsoTests
{
    private readonly Pbkdf2Sha512PasswordHashAlgorithm _pbkdf2 = new();

    [Fact]
    public void round_trip_with_pbkdf2_preserves_algorithm_id()
    {
        var original = HashedPassword.From("password", _pbkdf2);

        var restored = original.ToDso().ToValueObject();

        restored.AlgorithmId.ShouldBe(original.AlgorithmId);
    }

    [Fact]
    public void round_trip_with_pbkdf2_preserves_hash()
    {
        var original = HashedPassword.From("password", _pbkdf2);

        var restored = original.ToDso().ToValueObject();

        restored.Data.Hash.ShouldBe(original.Data.Hash);
    }

    [Fact]
    public void round_trip_with_pbkdf2_preserves_salt()
    {
        var original = HashedPassword.From("password", _pbkdf2);

        var restored = original.ToDso().ToValueObject();

        restored.Data.Salt.ShouldBe(original.Data.Salt);
    }

    [Fact]
    public void round_trip_with_pbkdf2_preserves_parameters()
    {
        var original = HashedPassword.From("password", _pbkdf2);

        var restored = original.ToDso().ToValueObject();

        restored.Data.Parameters.ShouldBe(original.Data.Parameters);
    }

    [Fact]
    public void round_trip_with_fake_algorithm_preserves_all_fields()
    {
        var fakeAlgorithm = new FakeAlgorithm();
        var original = HashedPassword.From("password", fakeAlgorithm);

        var restored = original.ToDso().ToValueObject();

        restored.AlgorithmId.ShouldBe("fake-algo");
        restored.Data.Hash.ShouldBe(original.Data.Hash);
        restored.Data.Salt.ShouldBe(original.Data.Salt);
        restored.Data.Parameters.ShouldBe(original.Data.Parameters);
    }

    [Fact]
    public void dso_with_null_parameters_round_trips_to_empty_dictionary()
    {
        var dso = new HashedPasswordDso.V1("fake-algo", Convert.ToBase64String([1, 2, 3]), Convert.ToBase64String([4, 5, 6]), null);

        var restored = dso.ToValueObject();

        restored.Data.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void verify_still_works_after_round_trip()
    {
        var original = HashedPassword.From("password", _pbkdf2);
        var restored = original.ToDso().ToValueObject();

        var result = _pbkdf2.Verify("password", restored.Data);

        result.ShouldBeTrue();
    }

    private sealed class FakeAlgorithm : IPasswordHashAlgorithm
    {
        public string AlgorithmId => "fake-algo";

        public HashedPasswordData Hash(string password) =>
            new("fake-algo", [1, 2, 3, 4], [5, 6, 7, 8], new Dictionary<string, string> { ["key"] = "value" });

        public bool Verify(string password, HashedPasswordData data) => true;

        public bool NeedsRehash(HashedPasswordData data) => data.AlgorithmId != AlgorithmId;
    }
}
