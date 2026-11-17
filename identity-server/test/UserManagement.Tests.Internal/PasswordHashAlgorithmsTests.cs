// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public sealed class PasswordHashAlgorithmsTests
{
    [Fact]
    public void throws_on_duplicate_algorithm_id()
    {
        var algorithms = new IPasswordHashAlgorithm[] { new FakeAlgo("dupe"), new FakeAlgo("dupe") };
        var options = Options.Create(new UserAuthenticationOptions());

        var ex = Should.Throw<InvalidOperationException>(() => new PasswordHashAlgorithms(algorithms, options));

        ShouldlyExtensions.ShouldContain(ex.Message, "'dupe'");
    }

    [Fact]
    public void throws_when_preferred_algorithm_not_found()
    {
        var algorithms = new IPasswordHashAlgorithm[] { new FakeAlgo("other") };
        var options = Options.Create(new UserAuthenticationOptions());

        _ = Should.Throw<InvalidOperationException>(() => new PasswordHashAlgorithms(algorithms, options));
    }

    private sealed class FakeAlgo(string algorithmId) : IPasswordHashAlgorithm
    {
        public string AlgorithmId => algorithmId;

        public HashedPasswordData Hash(string password) =>
            new(AlgorithmId, [1], [2], new Dictionary<string, string>());

        public bool Verify(string password, HashedPasswordData data) => true;

        public bool NeedsRehash(HashedPasswordData data) => data.AlgorithmId != AlgorithmId;
    }
}
