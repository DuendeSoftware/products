// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationFlowWithWebComponent.Pages;

public class SignInErrorModel : PageModel
{
    public string ErrorMessage { get; set; } = "An error occurred during sign in.";

    public void OnGet(string? error) => ErrorMessage = error switch
    {
        "CouldNotAddAuthenticator" => "Could not add authenticator to your account.",
        "AuthenticatorAlreadyExists" => "This authenticator is already associated with another account.",
        "UserNotFoundForAddAuthenticator" => "User account not found.",
        "CouldNotRegisterUser" => "Could not register new user account.",
        _ => "An error occurred during sign in. Please try again."
    };
}
