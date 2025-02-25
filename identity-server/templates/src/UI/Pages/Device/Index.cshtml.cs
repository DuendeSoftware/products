// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using IdentityServerHost.Pages.Consent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace IdentityServerHost.Pages.Device;

[SecurityHeaders]
[Authorize]
public class Index : PageModel
{
    private readonly IDeviceFlowInteractionService _interaction;
    private readonly IEventService _events;
    private readonly IOptions<IdentityServerOptions> _options;
    private readonly ILogger<Index> _logger;

    public Index(
        IDeviceFlowInteractionService interaction,
        IEventService eventService,
        IOptions<IdentityServerOptions> options,
        ILogger<Index> logger)
    {
        _interaction = interaction;
        _events = eventService;
        _options = options;
        _logger = logger;
    }

    public ViewModel View { get; set; } = default!;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public async Task<IActionResult> OnGet(string? userCode)
    {
        if (String.IsNullOrWhiteSpace(userCode))
        {
            return Page();
        }

        if (!await SetViewModelAsync(userCode))
        {
            ModelState.AddModelError("", DeviceOptions.InvalidUserCode);
            return Page();
        }

        Input = new InputModel { 
            UserCode = userCode,
        };

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var request = await _interaction.GetAuthorizationContextAsync(Input.UserCode ?? throw new ArgumentNullException(nameof(Input.UserCode)));
        if (request == null) return RedirectToPage("/Home/Error/Index");

        ConsentResponse? grantedConsent = null;

        // user clicked 'no' - send back the standard 'access_denied' response
        if (Input.Button == "no")
        {
            grantedConsent = new ConsentResponse
            {
                Error = AuthorizationError.AccessDenied
            };

            // emit event
            await _events.RaiseAsync(new ConsentDeniedEvent(User.GetSubjectId(), request.Client.ClientId, request.ValidatedResources.RawScopeValues));
            Telemetry.Metrics.ConsentDenied(request.Client.ClientId, request.ValidatedResources.ParsedScopes.Select(s => s.ParsedName));
        }
        // user clicked 'yes' - validate the data
        else if (Input.Button == "yes")
        {
            // if the user consented to some scope, build the response model
            if (Input.ScopesConsented.Any())
            {
                var scopes = Input.ScopesConsented;
                if (ConsentOptions.EnableOfflineAccess == false)
                {
                    scopes = scopes.Where(x => x != Duende.IdentityServer.IdentityServerConstants.StandardScopes.OfflineAccess);
                }

                grantedConsent = new ConsentResponse
                {
                    RememberConsent = Input.RememberConsent,
                    ScopesValuesConsented = scopes.ToArray(),
                    Description = Input.Description
                };

                // emit event
                await _events.RaiseAsync(new ConsentGrantedEvent(User.GetSubjectId(), request.Client.ClientId, request.ValidatedResources.RawScopeValues, grantedConsent.ScopesValuesConsented, grantedConsent.RememberConsent));
                Telemetry.Metrics.ConsentGranted(request.Client.ClientId, grantedConsent.ScopesValuesConsented, grantedConsent.RememberConsent);
                var denied = request.ValidatedResources.ParsedScopes.Select(s => s.ParsedName).Except(grantedConsent.ScopesValuesConsented);
                Telemetry.Metrics.ConsentDenied(request.Client.ClientId, denied);
            }
            else
            {
                ModelState.AddModelError("", ConsentOptions.MustChooseOneErrorMessage);
            }
        }
        else
        {
            ModelState.AddModelError("", ConsentOptions.InvalidSelectionErrorMessage);
        }

        if (grantedConsent != null)
        {
            // communicate outcome of consent back to identityserver
            await _interaction.HandleRequestAsync(Input.UserCode, grantedConsent);

            // indicate that's it ok to redirect back to authorization endpoint
            return RedirectToPage("/Device/Success");
        }

        // we need to redisplay the consent UI
        if (!await SetViewModelAsync(Input.UserCode))
        {
            return RedirectToPage("/Home/Error/Index");
        }
        return Page();
    }


    private async Task<bool> SetViewModelAsync(string userCode)
    {
        var request = await _interaction.GetAuthorizationContextAsync(userCode);
        if (request != null)
        {
            View = CreateConsentViewModel(request);
            return true;
        }
        else
        {
            View = new ViewModel();
            return false;
        }
    }

    private ViewModel CreateConsentViewModel(DeviceFlowAuthorizationRequest request)
    {
        var vm = new ViewModel
        {
            ClientName = request.Client.ClientName ?? request.Client.ClientId,
            ClientUrl = request.Client.ClientUri,
            ClientLogoUrl = request.Client.LogoUri,
            AllowRememberConsent = request.Client.AllowRememberConsent
        };

        vm.IdentityScopes = request.ValidatedResources.Resources.IdentityResources.Select(x => CreateScopeViewModel(x, Input == null || Input.ScopesConsented.Contains(x.Name))).ToArray();

        var apiScopes = new List<ScopeViewModel>();
        foreach (var parsedScope in request.ValidatedResources.ParsedScopes)
        {
            var apiScope = request.ValidatedResources.Resources.FindApiScope(parsedScope.ParsedName);
            if (apiScope != null)
            {
                var scopeVm = CreateScopeViewModel(parsedScope, apiScope, Input == null || Input.ScopesConsented.Contains(parsedScope.RawValue));
                apiScopes.Add(scopeVm);
            }
        }
        if (DeviceOptions.EnableOfflineAccess && request.ValidatedResources.Resources.OfflineAccess)
        {
            apiScopes.Add(GetOfflineAccessScope(Input == null || Input.ScopesConsented.Contains(Duende.IdentityServer.IdentityServerConstants.StandardScopes.OfflineAccess)));
        }
        vm.ApiScopes = apiScopes;

        return vm;
    }

    private static ScopeViewModel CreateScopeViewModel(IdentityResource identity, bool check)
    {
        return new ScopeViewModel
        {
            Value = identity.Name,
            DisplayName = identity.DisplayName ?? identity.Name,
            Description = identity.Description,
            Emphasize = identity.Emphasize,
            Required = identity.Required,
            Checked = check || identity.Required
        };
    }

    private static ScopeViewModel CreateScopeViewModel(ParsedScopeValue parsedScopeValue, ApiScope apiScope, bool check)
    {
        return new ScopeViewModel
        {
            Value = parsedScopeValue.RawValue,
            // todo: use the parsed scope value in the display?
            DisplayName = apiScope.DisplayName ?? apiScope.Name,
            Description = apiScope.Description,
            Emphasize = apiScope.Emphasize,
            Required = apiScope.Required,
            Checked = check || apiScope.Required
        };
    }

    private static ScopeViewModel GetOfflineAccessScope(bool check)
    {
        return new ScopeViewModel
        {
            Value = Duende.IdentityServer.IdentityServerConstants.StandardScopes.OfflineAccess,
            DisplayName = DeviceOptions.OfflineAccessDisplayName,
            Description = DeviceOptions.OfflineAccessDescription,
            Emphasize = true,
            Checked = check
        };
    }
}
