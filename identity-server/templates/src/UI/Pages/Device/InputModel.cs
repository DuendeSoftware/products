namespace IdentityServerHost.Pages.Device;

public class InputModel
{
    public string? Button { get; set; }
    public IEnumerable<string> ScopesConsented { get; set; } = new List<string>();
    public string? ReturnUrl { get; set; }
    public string? Description { get; set; }
    public string? UserCode { get; set; }
}
