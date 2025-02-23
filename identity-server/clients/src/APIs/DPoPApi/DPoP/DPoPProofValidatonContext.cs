
namespace DPoPApi;

public class DPoPProofValidatonContext
{
    /// <summary>
    /// The ASP.NET Core authentication scheme triggering the validation
    /// </summary>
    public string Scheme { get; set; }

    /// <summary>
    /// The HTTP URL to validate
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// The HTTP method to validate
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// The DPoP proof token to validate
    /// </summary>
    public string ProofToken { get; set; }

    /// <summary>
    /// The access token
    /// </summary>
    public string AccessToken { get; set; }
}
