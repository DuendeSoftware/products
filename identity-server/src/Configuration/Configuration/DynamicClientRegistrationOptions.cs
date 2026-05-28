// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration.Configuration;

/// <summary>
/// Options that control the behavior of the dynamic client registration (DCR) endpoint.
/// These options are nested under <c>IdentityServerConfigurationOptions.DynamicClientRegistration</c>
/// and apply to all clients registered through the DCR pipeline.
/// </summary>
/// <remarks>
/// Configure these options during application startup via
/// <c>builder.Services.AddIdentityServerConfiguration(options => { ... })</c>.
/// <para>
/// See <see href="https://docs.duendesoftware.com/identityserver/configuration/dcr">Dynamic Client Registration</see>
/// in the IdentityServer documentation for more details.
/// </para>
/// </remarks>
public class DynamicClientRegistrationOptions
{
    /// <summary>
    /// Gets or sets the lifetime of secrets generated for clients registered via dynamic client
    /// registration. When set, the generated client secret will expire after this duration and
    /// the expiration timestamp will be included in the registration response as
    /// <c>client_secret_expires_at</c>. When <see langword="null"/> (the default), generated
    /// secrets never expire and <c>client_secret_expires_at</c> is returned as <c>0</c>.
    /// </summary>
    /// <remarks>
    /// For most deployments, the default (no expiration) is sufficient. Set a lifetime if your
    /// security policy requires periodic secret rotation for dynamically registered clients.
    /// </remarks>
    public TimeSpan? SecretLifetime { get; set; }
}
