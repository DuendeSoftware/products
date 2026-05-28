// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Runtime.Serialization;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace Duende.IdentityServer.Internal.Saml.Sp.Exceptions
{
    /// <summary>
    /// Extended exception containing information about the status and status message SAML response.  
    /// </summary>
    [Serializable]
    internal class UnsuccessfulSamlOperationException : Saml2Exception
    {
        /// <summary>
        /// Status of the SAML2Response
        /// </summary>
        public Saml2StatusCode Status { get; set; }
        /// <summary>
        /// Status message of SAML2Response
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Second level status of SAML2Response
        /// </summary>
        public string SecondLevelStatus { get; set; }

        /// <summary>
        /// Ctor, bundling the Saml2 status codes and message into the exception message.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="statusCode">Status of the SAML2Response</param>
        /// <param name="statusMessage">Status message of SAML2Response</param>
        /// <param name="secondLevelStatus">Second level status of SAML2Response</param>
        public UnsuccessfulSamlOperationException(string message, Saml2StatusCode statusCode, string statusMessage, string secondLevelStatus) :
            base(message + "\n" +
                "  Saml2 Status Code: " + statusCode + "\n" +
                "  Saml2 Status Message: " + statusMessage + "\n" +
                "  Saml2 Second Level Status: " + secondLevelStatus)
        {
            Status = statusCode;
            StatusMessage = statusMessage;
            SecondLevelStatus = secondLevelStatus;
        }
        /// <summary>
        /// 
        /// </summary>
        public UnsuccessfulSamlOperationException() : base()
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public UnsuccessfulSamlOperationException(string message) : base(message)
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public UnsuccessfulSamlOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected UnsuccessfulSamlOperationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

    }
}
