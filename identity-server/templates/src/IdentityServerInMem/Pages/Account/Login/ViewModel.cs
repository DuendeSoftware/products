namespace IdentityServerHost.Pages.Account.Login;

public class ViewModel
{
    public bool AllowRememberLogin { get; set; } = true;
    public bool EnableLocalLogin { get; set; } = true;

    public IEnumerable<ViewModel.ExternalProvider> ExternalProviders { get; set; } = Enumerable.Empty<ExternalProvider>();
    public IEnumerable<ViewModel.ExternalProvider> VisibleExternalProviders => ExternalProviders.Where(x => !string.IsNullOrWhiteSpace(x.DisplayName));

    public bool IsExternalLoginOnly => EnableLocalLogin == false && ExternalProviders?.Count() == 1;
    public string? ExternalLoginScheme => IsExternalLoginOnly ? ExternalProviders?.SingleOrDefault()?.AuthenticationScheme : null;

    public class ExternalProvider(string authenticationScheme, string? displayName = null)
    {
        public string? DisplayName { get; set; } = displayName;
        public string AuthenticationScheme { get; set; } = authenticationScheme;
    }
}
