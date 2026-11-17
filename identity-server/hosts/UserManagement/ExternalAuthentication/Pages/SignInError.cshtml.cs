// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExternalAuthentication.Pages;

public class SignInErrorModel : PageModel
{
    public string ErrorTitle { get; set; } = "An error occurred";
    public string ErrorMessage { get; set; } = "An unexpected error occurred during sign-in.";

    public void OnGet(SignInError? error)
    {
        if (error.HasValue)
        {
            (ErrorTitle, ErrorMessage) = error.Value switch
            {
                SignInError.AuthenticatorAlreadyExists => (
                    "Authenticator Already Exists",
                    "This external account is already linked to your profile."
                ),
                SignInError.CouldNotAddAuthenticator => (
                    "Unable to Add Authenticator",
                    "We couldn't link this external account to your profile. This authenticator may already be linked to another account."
                ),
                SignInError.UserNotFoundForAddAuthenticator => (
                    "User Not Found",
                    "We couldn't find your existing account to link this authenticator. Please sign in again."
                ),
                SignInError.CouldNotCreateUser => (
                    "Registration Failed",
                    "We couldn't create your account. Please try again or contact support if the problem persists."
                ),
                _ => (ErrorTitle, ErrorMessage)
            };
        }
    }
}
