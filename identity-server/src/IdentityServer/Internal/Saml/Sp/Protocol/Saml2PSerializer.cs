// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Microsoft.IdentityModel.Tokens.Saml2;
using Microsoft.IdentityModel.Xml;
using static Microsoft.IdentityModel.Logging.LogHelper;
using EncryptingCredentials = Microsoft.IdentityModel.Tokens.EncryptingCredentials;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    internal static class LogMessages
    {
#pragma warning disable 1591
        // SamlSerializing reading
        internal const string IDX13102 = "IDX13102: Exception thrown while reading '{0}' for Saml2SecurityToken. Inner exception: '{1}'.";
        internal const string IDX13106 = "IDX13106: Unable to read for Saml2SecurityToken. Element: '{0}' as missing Attribute: '{1}'.";
        internal const string IDX13108 = "IDX13108: When reading '{0}', Assertion.Subject is null and no Statements were found. [Saml2Core, line 585].";
        internal const string IDX13109 = "IDX13109: When reading '{0}', Assertion.Subject is null and an Authentication, Attribute or AuthorizationDecision Statement was found. and no Statements were found. [Saml2Core, lines 1050, 1168, 1280].";
        internal const string IDX13137 = "IDX13137: Unable to read for Saml2SecurityToken. Version must be '2.0' was: '{0}'.";
        internal const string IDX13141 = "IDX13141: EncryptedAssertion is not supported. You will need to override ReadAssertion and provide support.";
        internal const string IDX13302 = "IDX13302: An assertion with no statements must contain a 'Subject' element.";
        internal const string IDX13303 = "IDX13303: 'Subject' is required in Saml2Assertion for built-in statement type.";
        internal const string IDX30213 = "IDX30213: The CryptoProviderFactory: '{0}', CreateForSigning returned null for key: '{1}', SignatureMethod: '{2}'.";
#pragma warning restore 1591
    }

    internal class Saml2EncryptedAssertion : Saml2Assertion
    {
        public Saml2EncryptedAssertion(Saml2NameIdentifier issuer) :
            base(issuer)
        {
        }

        public EncryptingCredentials EncryptingCredentials { get; set; }
    }

    // Contains overrides to:
    // - add support for encrypted assertions
    // - use a fixed version of EnvelopedSignatureWriter that does not write duplicate Id attributes
    // (reported as a bug in Microsoft.IdentityModel.*)
    // - ignore authentication context if configured to do so
    class Saml2PSerializer : Saml2Serializer
    {
        private SPOptions spOptions;

        public Saml2PSerializer(SPOptions spOptions)
        {
            this.spOptions = spOptions;
        }

        public ICollection<X509Certificate2> DecryptionCertificates { get; set; }

        /// <summary>
        /// Reads a &lt;saml:Assertion> element.
        /// </summary>
        /// <param name="reader">A <see cref="XmlReader"/> positioned at a <see cref="Saml2Assertion"/> element.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="reader"/> is null.</exception>
        /// <exception cref="NotSupportedException">if assertion is encrypted.</exception>
        /// <exception cref="Saml2SecurityTokenReadException">If <paramref name="reader"/> is not positioned at a Saml2Assertion.</exception>
        /// <exception cref="Saml2SecurityTokenReadException">If Version is not '2.0'.</exception>
        /// <exception cref="Saml2SecurityTokenReadException">If 'Id' is missing.</exception>>
        /// <exception cref="Saml2SecurityTokenReadException">If 'IssueInstant' is missing.</exception>>
        /// <exception cref="Saml2SecurityTokenReadException">If no statements are found.</exception>>
        /// <returns>A <see cref="Saml2Assertion"/> instance.</returns>
        public override Saml2Assertion ReadAssertion(XmlReader reader)
        {
            XmlUtil.CheckReaderOnEntry(reader, Saml2Constants.Elements.Assertion, Saml2Constants.Namespace);

            if (reader.IsStartElement(Saml2Constants.Elements.EncryptedAssertion, Saml2Constants.Namespace))
            {
                var encrypted = new XmlDocument();
                encrypted.PreserveWhitespace = true;
                encrypted.Load(reader);
                XmlElement decrypted = null;
                foreach (var cert in DecryptionCertificates)
                {
                    try
                    {
                        decrypted = encrypted.DocumentElement.Decrypt(cert.PrivateKey);
                        break;
                    }
                    catch (CryptographicException)
                    {
                    }
                }

                if (decrypted == null)
                {
                    throw new InvalidOperationException(
                        "Encrypted assertion could not be decrypted using any available decryption certificate");
                }

                reader = new XmlNodeReader(decrypted);
            }

            var envelopeReader = new EnvelopedSignatureReader(reader);
            var assertion = new Saml2Assertion(new Saml2NameIdentifier("__TemporaryIssuer__"));
            try
            {
                // @xsi:type
                XmlUtil.ValidateXsiType(envelopeReader, Saml2Constants.Types.AssertionType, Saml2Constants.Namespace);

                // @Version - required - must be "2.0"
                string version = envelopeReader.GetAttribute(Saml2Constants.Attributes.Version);
                if (string.IsNullOrEmpty(version))
                    throw LogReadException(LogMessages.IDX13106, Saml2Constants.Elements.Assertion, Saml2Constants.Attributes.Version);

                if (!StringComparer.Ordinal.Equals(Saml2Constants.Version, version))
                    throw LogReadException(LogMessages.IDX13137, version);

                // @ID - required
                string value = envelopeReader.GetAttribute(Saml2Constants.Attributes.ID);
                if (string.IsNullOrEmpty(value))
                    throw LogReadException(LogMessages.IDX13106, Saml2Constants.Elements.Assertion, Saml2Constants.Attributes.ID);

                assertion.Id = new Saml2Id(value);

                // @IssueInstant - required
                value = envelopeReader.GetAttribute(Saml2Constants.Attributes.IssueInstant);
                if (string.IsNullOrEmpty(value))
                    throw LogReadException(LogMessages.IDX13106, Saml2Constants.Elements.Assertion, Saml2Constants.Attributes.IssueInstant);

                assertion.IssueInstant = DateTime.ParseExact(value, Saml2Constants.AcceptedDateTimeFormats, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.None).ToUniversalTime();

                // will move to next element
                // <ds:Signature> 0-1 read by EnvelopedSignatureReader
                envelopeReader.Read();

                // <Issuer> 1
                assertion.Issuer = ReadIssuer(envelopeReader);

                // <Subject> 0-1
                if (envelopeReader.IsStartElement(Saml2Constants.Elements.Subject, Saml2Constants.Namespace))
                    assertion.Subject = ReadSubject(envelopeReader);

                // <Conditions> 0-1
                if (envelopeReader.IsStartElement(Saml2Constants.Elements.Conditions, Saml2Constants.Namespace))
                    assertion.Conditions = ReadConditions(envelopeReader);

                // <Advice> 0-1
                if (envelopeReader.IsStartElement(Saml2Constants.Elements.Advice, Saml2Constants.Namespace))
                    assertion.Advice = ReadAdvice(envelopeReader);

                // <Statement|AuthnStatement|AuthzDecisionStatement|AttributeStatement>, 0-OO
                while (envelopeReader.IsStartElement())
                {
                    Saml2Statement statement;

                    if (envelopeReader.IsStartElement(Saml2Constants.Elements.Statement, Saml2Constants.Namespace))
                        statement = ReadStatement(envelopeReader);
                    else if (envelopeReader.IsStartElement(Saml2Constants.Elements.AttributeStatement, Saml2Constants.Namespace))
                        statement = ReadAttributeStatement(envelopeReader);
                    else if (envelopeReader.IsStartElement(Saml2Constants.Elements.AuthnStatement, Saml2Constants.Namespace))
                        statement = ReadAuthenticationStatement(envelopeReader);
                    else if (envelopeReader.IsStartElement(Saml2Constants.Elements.AuthzDecisionStatement, Saml2Constants.Namespace))
                        statement = ReadAuthorizationDecisionStatement(envelopeReader);
                    else
                        break;

                    assertion.Statements.Add(statement);
                }

                envelopeReader.ReadEndElement();
                if (assertion.Subject == null)
                {
                    // An assertion with no statements MUST contain a <Subject> element. [Saml2Core, line 585]
                    if (0 == assertion.Statements.Count)
                        throw LogReadException(LogMessages.IDX13108, Saml2Constants.Elements.Assertion);

                    // Furthermore, the built-in statement types all require the presence of a subject.
                    // [Saml2Core, lines 1050, 1168, 1280]
                    foreach (Saml2Statement statement in assertion.Statements)
                    {
                        if (statement is Saml2AuthenticationStatement
                            || statement is Saml2AttributeStatement
                            || statement is Saml2AuthorizationDecisionStatement)
                        {
                            throw LogReadException(LogMessages.IDX13109, Saml2Constants.Elements.Assertion);
                        }
                    }
                }

                // attach signedXml for validation of signature
                assertion.Signature = envelopeReader.Signature;
                return assertion;
            }
            catch (Exception ex)
            {
                if (ex is Saml2SecurityTokenReadException)
                    throw;

                throw LogReadException(LogMessages.IDX13102, ex, Saml2Constants.Elements.Assertion, ex);
            }
        }

        public virtual void WriteEncryptedAssertion(XmlWriter writer, Saml2EncryptedAssertion assertion)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            using (var xw = doc.CreateNavigator().AppendChild())
            {
                base.WriteAssertion(xw, assertion);
            }

            doc.DocumentElement.Encrypt(assertion.EncryptingCredentials);
            writer.WriteStartElement("EncryptedAssertion", "urn:oasis:names:tc:SAML:2.0:assertion");
            doc.DocumentElement.WriteTo(writer);
            writer.WriteEndElement();
        }

        public override void WriteAssertion(XmlWriter writer, Saml2Assertion assertion)
        {
            if (writer == null)
                throw LogArgumentNullException(nameof(writer));

            if (assertion == null)
                throw LogArgumentNullException(nameof(assertion));

            if (assertion is Saml2EncryptedAssertion encryptedAssertion &&
                encryptedAssertion.EncryptingCredentials != null)
            {
                WriteEncryptedAssertion(writer, encryptedAssertion);
                return;
            }

            // Wrap the writer if necessary for a signature
            // We do not dispose this writer, since as a delegating writer it would
            // dispose the inner writer, which we don't properly own.
            EnvelopedSignatureWriterWithReferenceIdFix signatureWriter = null;
            if (assertion.SigningCredentials != null)
                writer = signatureWriter = new EnvelopedSignatureWriterWithReferenceIdFix(writer, assertion.SigningCredentials, assertion.Id.Value, assertion.InclusiveNamespacesPrefixList) { DSigSerializer = DSigSerializer };

            if (assertion.Subject == null)
            {
                // An assertion with no statements MUST contain a <Subject> element. [Saml2Core, line 585]
                if (assertion.Statements.Count == 0)
                    throw LogExceptionMessage(new Saml2SecurityTokenException(LogMessages.IDX13302));

                // Furthermore, the built-in statement types all require the presence of a subject.
                // [Saml2Core, lines 1050, 1168, 1280]
                foreach (Saml2Statement statement in assertion.Statements)
                {
                    if (statement is Saml2AuthenticationStatement
                        || statement is Saml2AttributeStatement
                        || statement is Saml2AuthorizationDecisionStatement)
                    {
                        throw LogExceptionMessage(new Saml2SecurityTokenException(LogMessages.IDX13303));
                    }
                }
            }

            // <Assertion>
            writer.WriteStartElement(Prefix, Saml2Constants.Elements.Assertion, Saml2Constants.Namespace);

            // @ID - required
            writer.WriteAttributeString(Saml2Constants.Attributes.ID, assertion.Id.Value);

            // @IssueInstant - required
            writer.WriteAttributeString(Saml2Constants.Attributes.IssueInstant, assertion.IssueInstant.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));

            // @Version - required
            writer.WriteAttributeString(Saml2Constants.Attributes.Version, assertion.Version);

            // <Issuer> 1
            WriteIssuer(writer, assertion.Issuer);

            // <Signature> 0-1
            if (null != signatureWriter)
                signatureWriter.WriteSignature();

            // <Subject> 0-1
            if (null != assertion.Subject)
                WriteSubject(writer, assertion.Subject);

            // <Conditions> 0-1
            if (null != assertion.Conditions)
                WriteConditions(writer, assertion.Conditions);

            // <Advice> 0-1
            if (null != assertion.Advice)
                WriteAdvice(writer, assertion.Advice);

            // <Statement|AuthnStatement|AuthzDecisionStatement|AttributeStatement>, 0-OO
            foreach (Saml2Statement statement in assertion.Statements)
                WriteStatement(writer, statement);

            writer.WriteEndElement();
        }

        protected override Saml2AuthenticationContext ReadAuthenticationContext(XmlDictionaryReader reader)
        {
            if (spOptions?.Compatibility?.IgnoreAuthenticationContextInResponse ?? false)
            {
                reader.Skip();
                //hack to get around the lack of a sane constructor
                return (Saml2AuthenticationContext)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Saml2AuthenticationContext));
            }

            return base.ReadAuthenticationContext(reader);
        }

        internal static Exception LogReadException(string message)
        {
            return LogExceptionMessage(new Saml2SecurityTokenReadException(message));
        }

        internal static Exception LogReadException(string message, Exception ex)
        {
            return LogExceptionMessage(new Saml2SecurityTokenReadException(message, ex));
        }

        internal static Exception LogReadException(string format, params object[] args)
        {
            return LogExceptionMessage(new Saml2SecurityTokenReadException(FormatInvariant(format, args)));
        }

        internal static Exception LogReadException(string format, Exception inner, params object[] args)
        {
            return LogExceptionMessage(new Saml2SecurityTokenReadException(FormatInvariant(format, args), inner));
        }
    }
}
