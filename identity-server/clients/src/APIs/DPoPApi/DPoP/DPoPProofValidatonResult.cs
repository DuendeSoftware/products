using System.Collections.Generic;

namespace DPoPApi;

public class DPoPProofValidatonResult
{
    public static DPoPProofValidatonResult Success = new DPoPProofValidatonResult { IsError = false };

    /// <summary>
    /// Indicates if the result was successful or not
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// The error code for the validation result
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// The error description code for the validation result
    /// </summary>
    public string ErrorDescription { get; set; }

    /// <summary>
    /// The serialized JWK from the validated DPoP proof token.
    /// </summary>
    public string JsonWebKey { get; set; }

    /// <summary>
    /// The JWK thumbprint from the validated DPoP proof token.
    /// </summary>
    public string JsonWebKeyThumbprint { get; set; }

    /// <summary>
    /// The cnf value for the DPoP proof token 
    /// </summary>
    public string Confirmation { get; set; }

    /// <summary>
    /// The payload value of the DPoP proof token.
    /// </summary>
    public IDictionary<string, object> Payload { get; internal set; }

    /// <summary>
    /// The jti value read from the payload.
    /// </summary>
    public string TokenId { get; set; }
    
    /// <summary>
    /// The ath value read from the payload.
    /// </summary>
    public string AccessTokenHash { get; set; }

    /// <summary>
    /// The nonce value read from the payload.
    /// </summary>
    public string Nonce { get; set; }

    /// <summary>
    /// The iat value read from the payload.
    /// </summary>
    public long? IssuedAt { get; set; }

    /// <summary>
    /// The nonce value issued by the server.
    /// </summary>
    public string ServerIssuedNonce { get; set; }
}
