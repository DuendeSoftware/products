// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Test;

/// <summary>
/// Resource owner password validator for test users
/// </summary>
/// <seealso cref="IResourceOwnerPasswordValidator" />
public class TestUserResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
{
    private readonly TestUserStore _users;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestUserResourceOwnerPasswordValidator"/> class.
    /// </summary>
    /// <param name="users">The users.</param>
    /// <param name="timeProvider">The time provider.</param>
    public TestUserResourceOwnerPasswordValidator(TestUserStore users, TimeProvider timeProvider)
    {
        _users = users;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates the resource owner password credential
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    public Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
    {
        if (_users.ValidateCredentials(context.UserName, context.Password))
        {
            var user = _users.FindByUsername(context.UserName);
            context.Result = new GrantValidationResult(
                user.SubjectId ?? throw new ArgumentException("Subject ID not set", nameof(user.SubjectId)),
                OidcConstants.AuthenticationMethods.Password, _timeProvider.GetUtcNow().UtcDateTime,
                user.Claims);
        }

        return Task.CompletedTask;
    }
}
