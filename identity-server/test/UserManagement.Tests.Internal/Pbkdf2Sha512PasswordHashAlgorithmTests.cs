// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;

namespace Duende.Platform.UserManagement;

public sealed class Pbkdf2Sha512PasswordHashAlgorithmTests
{
    private readonly Pbkdf2Sha512PasswordHashAlgorithm _sut = new();

    [Fact]
    public void hash_returns_correct_algorithm_id()
    {
        var result = _sut.Hash("password");

        result.AlgorithmId.ShouldBe(Pbkdf2PasswordConstants.AlgorithmId);
    }

    [Fact]
    public void hash_returns_non_empty_hash_and_salt()
    {
        var result = _sut.Hash("password");

        result.Hash.Count.ShouldBeGreaterThan(0);
        result.Salt.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void hash_returns_expected_parameters()
    {
        var result = _sut.Hash("password");

        result.Parameters.ShouldContainKey(Pbkdf2PasswordConstants.ParamPrf);
        result.Parameters[Pbkdf2PasswordConstants.ParamPrf].ShouldBe("SHA512");
        result.Parameters.ShouldContainKey(Pbkdf2PasswordConstants.ParamIterations);
        result.Parameters[Pbkdf2PasswordConstants.ParamIterations].ShouldBe("210000");
        result.Parameters.ShouldContainKey(Pbkdf2PasswordConstants.ParamDigestSize);
        result.Parameters[Pbkdf2PasswordConstants.ParamDigestSize].ShouldBe("64");
    }

    [Fact]
    public void hash_produces_different_salts_on_each_call()
    {
        var first = _sut.Hash("password");
        var second = _sut.Hash("password");

        first.Salt.ShouldNotBe(second.Salt);
    }

    [Fact]
    public void verify_returns_true_for_correct_password()
    {
        var data = _sut.Hash("correct-password");

        var result = _sut.Verify("correct-password", data);

        result.ShouldBeTrue();
    }

    [Fact]
    public void verify_returns_false_for_wrong_password()
    {
        var data = _sut.Hash("correct-password");

        var result = _sut.Verify("wrong-password", data);

        result.ShouldBeFalse();
    }

    [Fact]
    public void verify_returns_false_for_empty_password_when_hashed_non_empty()
    {
        var data = _sut.Hash("correct-password");

        var result = _sut.Verify("", data);

        result.ShouldBeFalse();
    }

    [Fact]
    public void hash_produces_64_byte_digest()
    {
        var result = _sut.Hash("password");

        result.Hash.Count.ShouldBe(64);
    }

    [Fact]
    public void hash_produces_16_byte_salt()
    {
        var result = _sut.Hash("password");

        result.Salt.Count.ShouldBe(16);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10000001")]
    public void verify_returns_false_for_out_of_bounds_iterations(string iterations)
    {
        var data = _sut.Hash("password");
        var tampered = new HashedPasswordData(
            data.AlgorithmId,
            data.Hash,
            data.Salt,
            new Dictionary<string, string>(data.Parameters)
            {
                [Pbkdf2PasswordConstants.ParamIterations] = iterations
            });

        _sut.Verify("password", tampered).ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("513")]
    public void verify_returns_false_for_out_of_bounds_digest_size(string digestSize)
    {
        var data = _sut.Hash("password");
        var tampered = new HashedPasswordData(
            data.AlgorithmId,
            data.Hash,
            data.Salt,
            new Dictionary<string, string>(data.Parameters)
            {
                [Pbkdf2PasswordConstants.ParamDigestSize] = digestSize
            });

        _sut.Verify("password", tampered).ShouldBeFalse();
    }

    [Theory]
    [InlineData("HMACSHA512")]
    [InlineData("unknown")]
    [InlineData("")]
    public void verify_returns_false_for_invalid_prf(string prf)
    {
        var data = _sut.Hash("password");
        var tampered = new HashedPasswordData(
            data.AlgorithmId,
            data.Hash,
            data.Salt,
            new Dictionary<string, string>(data.Parameters)
            {
                [Pbkdf2PasswordConstants.ParamPrf] = prf
            });

        _sut.Verify("password", tampered).ShouldBeFalse();
    }

    [Fact]
    public void verify_returns_false_when_prf_missing()
    {
        var data = _sut.Hash("password");
        var paramsWithoutPrf = new Dictionary<string, string>(data.Parameters);
        _ = paramsWithoutPrf.Remove(Pbkdf2PasswordConstants.ParamPrf);
        var tampered = new HashedPasswordData(data.AlgorithmId, data.Hash, data.Salt, paramsWithoutPrf);

        _sut.Verify("password", tampered).ShouldBeFalse();
    }

    [Fact]
    public void needs_rehash_returns_false_for_current_parameters()
    {
        var data = _sut.Hash("password");

        _sut.NeedsRehash(data).ShouldBeFalse();
    }

    [Fact]
    public void needs_rehash_returns_true_for_different_algorithm_id()
    {
        var data = new HashedPasswordData("other-algo", [1, 2], [3, 4], new Dictionary<string, string>());

        _sut.NeedsRehash(data).ShouldBeTrue();
    }

    [Fact]
    public void needs_rehash_returns_true_for_different_prf()
    {
        var data = _sut.Hash("password");
        var modified = new HashedPasswordData(
            data.AlgorithmId, data.Hash, data.Salt,
            new Dictionary<string, string>(data.Parameters) { [Pbkdf2PasswordConstants.ParamPrf] = "SHA256" });

        _sut.NeedsRehash(modified).ShouldBeTrue();
    }

    [Fact]
    public void needs_rehash_returns_true_for_lower_iterations()
    {
        var data = _sut.Hash("password");
        var modified = new HashedPasswordData(
            data.AlgorithmId, data.Hash, data.Salt,
            new Dictionary<string, string>(data.Parameters) { [Pbkdf2PasswordConstants.ParamIterations] = "10000" });

        _sut.NeedsRehash(modified).ShouldBeTrue();
    }

    [Fact]
    public void needs_rehash_returns_true_for_different_digest_size()
    {
        var data = _sut.Hash("password");
        var modified = new HashedPasswordData(
            data.AlgorithmId, data.Hash, data.Salt,
            new Dictionary<string, string>(data.Parameters) { [Pbkdf2PasswordConstants.ParamDigestSize] = "32" });

        _sut.NeedsRehash(modified).ShouldBeTrue();
    }
}
