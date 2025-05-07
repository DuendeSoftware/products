// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics.Metrics;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer;


/// <summary>
/// Telemetry helpers
/// </summary>
public static class Telemetry
{
    private readonly static string ServiceVersion = typeof(Telemetry).Assembly.GetName().Version.ToString();

    /// <summary>
    /// Service name used for Duende IdentityServer.
    /// </summary>
    public const string ServiceName = "Duende.IdentityServer";

    /// <summary>
    /// Metrics configuration.
    /// </summary>
    public static class Metrics
    {
#pragma warning disable 1591

        /// <summary>
        /// Name of counters.
        /// </summary>
        public static class Counters
        {
            public const string Operation = "tokenservice.operation";
            public const string ActiveRequests = "tokenservice.active_requests";
            public const string ApiSecretValidation = "tokenservice.api.secret_validation";
            public const string BackchannelAuthentication = "tokenservice.backchannel_authentication";
            public const string ClientConfigValidation = "tokenservice.client.config_validation";
            public const string ClientSecretValidation = "tokenservice.client.secret_validation";
            public const string DeviceAuthentication = "tokenservice.device_authentication";
            public const string DynamicIdentityProviderValidation = "tokenservice.dynamic_identityprovider.validation";
            public const string Introspection = "tokenservice.introspection";
            public const string PushedAuthorizationRequest = "tokenservice.pushed_authorization_request";
            public const string ResourceOwnerAuthentication = "tokenservice.resourceowner_authentication";
            public const string Revocation = "tokenservice.revocation";
            public const string TokenIssued = "tokenservice.token_issued";
        }

        /// <summary>
        /// Name of tags
        /// </summary>
        public static class Tags
        {
            public const string Api = "api";
            public const string Active = "active";
            public const string AuthMethod = "auth_method";
            public const string Caller = "caller";
            public const string Client = "client";
            public const string Endpoint = "endpoint";
            public const string Error = "error";
            public const string GrantType = "grant_type";
            public const string Method = "method";
            public const string Path = "path";
            public const string AuthorizeRequestType = "authorize_request_type";
            public const string Scheme = "scheme";
            public const string Result = "result";
            public const string Type = "type";
        }

        /// <summary>
        /// Values for tags
        /// </summary>
        public static class TagValues
        {
            public const string Success = "success";
            public const string Error = "error";
            public const string InternalError = "internal_error";
        }

#pragma warning restore 1591

        /// <summary>
        /// Meter for IdentityServer
        /// </summary>
        public static readonly Meter Meter = new Meter(ServiceName, ServiceVersion);

        /// <summary>
        /// Counter for active requests.
        /// </summary>
        public static readonly UpDownCounter<long> ActiveRequestCounter = Meter.CreateUpDownCounter<long>(Counters.ActiveRequests);

        /// <summary>
        /// Increase <see cref="ActiveRequestCounter"/>
        /// </summary>
        /// <param name="endpointType">Type name for endpoint</param>
        /// <param name="path">Path</param>
        public static void IncreaseActiveRequests(string endpointType, string path) =>
            ActiveRequestCounter.Add(1, new(Tags.Endpoint, endpointType), new(Tags.Path, path));

        /// <summary>
        /// Decrease <see cref="ActiveRequestCounter"/>
        /// </summary>
        /// <param name="endpointType">Type name for endpoint</param>
        /// <param name="path">Path</param>
        public static void DecreaseActiveRequests(string endpointType, string path) =>
            ActiveRequestCounter.Add(-1, new(Tags.Endpoint, endpointType), new(Tags.Path, path));

        /// <summary>
        /// High level number of operations and result/outcome
        /// </summary>
        public static readonly Counter<long> OperationCounter = Meter.CreateCounter<long>(Counters.Operation);

        /// <summary>
        /// Helper method to increase the <see cref="OperationCounter"/> with a success event
        /// </summary>
        /// <param name="clientId">Client involved in event</param>
        public static void Success(string clientId = null)
        {
            if (clientId != null)
            {
                OperationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Result, TagValues.Success));
            }
            else
            {
                OperationCounter.Add(1, tag: new(Tags.Result, TagValues.Success));
            }
        }

        /// <summary>
        /// Helper method to increase the <see cref="OperationCounter"/> with an error event
        /// </summary>
        /// <param name="error">Error message</param>
        /// <param name="clientId">Client involved in event</param>
        public static void Failure(string error, string clientId = null)
        {
            if (clientId != null)
            {
                OperationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, error), new(Tags.Result, TagValues.Error));
            }
            else
            {
                OperationCounter.Add(1, new(Tags.Error, error), new(Tags.Result, TagValues.Error));
            }
        }

        /// <summary>
        /// Helper method to increase <see cref="OperationCounter"/> on internal errors
        /// </summary>
        /// <param name="ex">Exception</param>
        public static void UnHandledException(Exception ex)
            => OperationCounter.Add(1,
                new(Tags.Type, ex.GetType().Name),
                new(Tags.Method, ex.TargetSite.Name),
                new(Tags.Result, TagValues.InternalError));

        /// <summary>
        /// Successful Api Secret validations
        /// </summary>
        public static Counter<long> ApiSecretValidationCounter
            = Meter.CreateCounter<long>(Counters.ApiSecretValidation);

        /// <summary>
        /// Helper method to increase <see cref="ApiSecretValidationCounter"/>
        /// </summary>
        /// <param name="apiId">Api Id</param>
        /// <param name="authMethod">Authentication Method</param>
        public static void ApiSecretValidation(string apiId, string authMethod)
        {
            Success(apiId);
            ApiSecretValidationCounter.Add(1, new(Tags.Api, apiId), new(Tags.AuthMethod, authMethod));
        }

        /// <summary>
        /// Helper method to increase <see cref="ApiSecretValidationFailure"/> on errors
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="message">Error message</param>
        public static void ApiSecretValidationFailure(string clientId, string message)
        {
            Failure(message, clientId);
            ApiSecretValidationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, message));
        }

        /// <summary>
        /// Successful back channel (CIBA) authentications counter
        /// </summary>
        public static readonly Counter<long> BackChannelAuthenticationCounter =
            Meter.CreateCounter<long>(Counters.BackchannelAuthentication);

        /// <summary>
        /// Helper method to increase <see cref="BackChannelAuthenticationCounter"/>
        /// </summary>
        /// <param name="clientId">Client Id</param>
        public static void BackChannelAuthentication(string clientId)
        {
            Success(clientId);
            BackChannelAuthenticationCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="BackChannelAuthenticationCounter"/> on errors
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="error"></param>
        public static void BackChannelAuthenticationFailure(string clientId, string error)
        {
            Failure(error, clientId);
            BackChannelAuthenticationCounter
                .Add(1, new(Tags.Client, clientId), new(Tags.Error, error));
        }

        /// <summary>
        /// Client configuration validation
        /// </summary>
        public static Counter<long> ClientValidationCounter =
            Meter.CreateCounter<long>(Counters.ClientConfigValidation);

        /// <summary>
        /// Helper method to increase <see cref="ClientValidationCounter"/>
        /// </summary>
        /// <param name="clientId"></param>
        public static void ClientValidation(string clientId)
        {
            Success(clientId);
            ClientValidationCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="ClientValidationCounter"/> on errors
        /// </summary>
        /// <param name="clientId">Client id</param>
        /// <param name="error">Error</param>
        public static void ClientValidationFailure(string clientId, string error)
        {
            Failure(error, clientId);
            ClientValidationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, error));
        }

        /// <summary>
        /// Successful Client Secret validations
        /// </summary>
        public static Counter<long> ClientSecretValidationCounter =
            Meter.CreateCounter<long>(Counters.ClientSecretValidation);

        /// <summary>
        /// Helper method to increase <see cref="ClientSecretValidationCounter"/>
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="authMethod"></param>
        public static void ClientSecretValidation(string clientId, string authMethod)
        {
            Success(clientId);
            ClientSecretValidationCounter.Add(1, new(Tags.Client, clientId), new(Tags.AuthMethod, authMethod));
        }

        /// <summary>
        /// Helper method to increase <see cref="ClientSecretValidationCounter"/> on failure.
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="message">Error message</param>
        public static void ClientSecretValidationFailure(string clientId, string message)
        {
            Failure(message, clientId);
            ClientSecretValidationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, message));
        }

        /// <summary>
        /// Successful device code authentication counter
        /// </summary>
        public static readonly Counter<long> DeviceAuthenticationCounter =
            Meter.CreateCounter<long>(Counters.DeviceAuthentication);

        /// <summary>
        /// Helper method to increase <see cref="DeviceAuthenticationCounter"/>
        /// </summary>
        /// <param name="clientId">Client ID</param>
        public static void DeviceAuthentication(string clientId)
        {
            Success(clientId);
            DeviceAuthenticationCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="DeviceAuthenticationCounter"/> on error
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="error">Error</param>
        public static void DeviceAuthenticationFailure(string clientId, string error)
        {
            Failure(error, clientId);
            DeviceAuthenticationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, error));
        }

        /// <summary>
        /// Dynamic identityprovider validations
        /// </summary>
        public static Counter<long> DynamicIdentityProviderValidationCounter
            = Meter.CreateCounter<long>(Counters.DynamicIdentityProviderValidation);

        /// <summary>
        /// Helper method to increase <see cref="DynamicIdentityProviderValidationCounter"/>
        /// </summary>
        /// <param name="scheme"></param>
        public static void DynamicIdentityProviderValidation(string scheme)
        {
            Success();
            DynamicIdentityProviderValidationCounter.Add(1, tag: new(Tags.Scheme, scheme));
        }

        /// <summary>
        /// Helper method to increase <see cref="DynamicIdentityProviderValidationCounter"/> on errors
        /// </summary>
        /// <param name="scheme">Scheme name</param>
        /// <param name="error">Error message</param>
        public static void DynamicIdentityProviderValidationFailure(string scheme, string error)
        {
            Failure(error);
            DynamicIdentityProviderValidationCounter
                .Add(1, new(Tags.Scheme, scheme), new(Tags.Error, error));
        }

        /// <summary>
        /// Introspection success counter
        /// </summary>
        public static readonly Counter<long> IntrospectionCounter =
            Meter.CreateCounter<long>(Counters.Introspection);

        /// <summary>
        /// Helper method to increase <see cref="IntrospectionCounter"/>
        /// </summary>
        /// <param name="callerId"></param>
        /// <param name="active">Is the token valid/active?</param>
        public static void Introspection(string callerId, bool active)
        {
            Success(callerId);
            IntrospectionCounter.Add(1, new(Tags.Caller, callerId), new(Tags.Active, active));
        }

        /// <summary>
        /// Helper method to increase <see cref="IntrospectionCounter"/> on errors
        /// </summary>
        /// <param name="callerId">Api resource or client Id</param>
        /// <param name="error">Error message</param>
        public static void IntrospectionFailure(string callerId, string error)
        {
            Failure(error, callerId);
            IntrospectionCounter.Add(1, new(Tags.Caller, callerId), new(Tags.Error, error));
        }

        /// <summary>
        /// Pushed Authorization Request Counter
        /// </summary>
        public static Counter<long> PushedAuthorizationRequestCounter
            = Meter.CreateCounter<long>(Counters.PushedAuthorizationRequest);

        /// <summary>
        /// Helper method to increase <see cref="PushedAuthorizationRequestCounter"/>
        /// </summary>
        /// <param name="clientId"></param>
        public static void PushedAuthorizationRequest(string clientId)
        {
            Success(clientId);
            PushedAuthorizationRequestCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="PushedAuthorizationRequestCounter"/> on errors.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="error">Error reason</param>
        public static void PushedAuthorizationRequestFailure(string clientId, string error)
        {
            Failure(clientId);
            PushedAuthorizationRequestCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, error));
        }

        /// <summary>
        /// Resource Owner Authentication Counter
        /// </summary>
        public static Counter<long> ResourceOwnerAuthenticationCounter
            = Meter.CreateCounter<long>(Counters.ResourceOwnerAuthentication);

        /// <summary>
        /// Helper method to increase <see cref="ResourceOwnerAuthenticationCounter"/>
        /// </summary>
        /// <param name="clientId"></param>
        public static void ResourceOwnerAuthentication(string clientId)
        {
            Success(clientId);
            ResourceOwnerAuthenticationCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="ResourceOwnerAuthenticationCounter"/> on errors
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="message">Error message</param>
        public static void ResourceOwnerAuthenticationFailure(string clientId, string message)
        {
            Failure(message, clientId);
            ResourceOwnerAuthenticationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, message));
        }

        /// <summary>
        /// Revocation success counter.
        /// </summary>
        public static readonly Counter<long> RevocationCounter =
            Meter.CreateCounter<long>(Counters.Revocation);

        /// <summary>
        /// Helper method to increase <see cref="RevocationCounter"/>
        /// </summary>
        /// <param name="clientId">Client id</param>
        public static void Revocation(string clientId)
        {
            Success(clientId);
            RevocationCounter.Add(1, tag: new(Tags.Client, clientId));
        }

        /// <summary>
        /// Helper method to increase <see cref="RevocationCounter"/> on errors.
        /// </summary>
        /// <param name="clientId">Client id</param>
        /// <param name="error">Error</param>
        public static void RevocationFailure(string clientId, string error)
        {
            Failure(error, clientId);
            RevocationCounter.Add(1, new(Tags.Client, clientId), new(Tags.Error, error));
        }

        /// <summary>
        /// Successful token issuance counter.
        /// </summary>
        public static readonly Counter<long> TokenIssuedCounter =
            Meter.CreateCounter<long>(Counters.TokenIssued);

        /// <summary>
        /// Helper method to increase <see cref="TokenIssuedCounter"/>
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="grantType">Grant Type</param>
        /// <param name="requestType">Type of authorization request</param>
        public static void TokenIssued(string clientId, string grantType, AuthorizeRequestType? requestType)
        {
            Success(clientId);
            TokenIssuedCounter.Add(1,
                new(Tags.Client, clientId),
                new(Tags.GrantType, grantType),
                new(Tags.AuthorizeRequestType, requestType));
        }

        /// <summary>
        /// Helper method to increase <see cref="TokenIssuedCounter"/> on errors
        /// </summary>
        /// <param name="clientId">Client Id</param>
        /// <param name="grantType">Grant Type</param>
        /// <param name="error">Error</param>
        /// <param name="requestType">Type of authorization request</param>
        public static void TokenIssuedFailure(string clientId, string grantType, AuthorizeRequestType? requestType, string error)
        {
            Failure(error, clientId);
            TokenIssuedCounter.Add(1,
                new(Tags.Client, clientId),
                new(Tags.GrantType, grantType),
                new(Tags.AuthorizeRequestType, requestType),
                new(Tags.Error, error));
        }
    }
}
