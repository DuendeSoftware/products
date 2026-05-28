// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Text;
using System.Xml;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Xml;
using static Microsoft.IdentityModel.Logging.LogHelper;

namespace Duende.IdentityServer.Internal.Saml.Sp.Protocol
{
    /// <summary>
    /// Wraps a <see cref="XmlWriter"/> and generates a signature automatically when the envelope
    /// is written completely. By default the generated signature is inserted as
    /// the last element in the envelope. This can be modified by explicitly
    /// calling WriteSignature to indicate the location inside the envelope where
    /// the signature should be inserted.
    /// </summary>
    internal class EnvelopedSignatureWriterWithReferenceIdFix : DelegatingXmlDictionaryWriter
    {
        private MemoryStream _canonicalStream;
        private bool _disposed;
        private DSigSerializer _dsigSerializer = DSigSerializer.Default;
        private int _elementCount;
        private string _inclusiveNamespacesPrefixList;
        private XmlWriter _originalWriter;
        private string _referenceId;
        private long _signaturePosition;
        private SigningCredentials _signingCredentials;
        private MemoryStream _writerStream;

        /// <summary>
        /// Initializes an instance of <see cref="EnvelopedSignatureWriter"/>. The returned writer can be directly used
        /// to write the envelope. The signature will be automatically generated when
        /// the envelope is completed.
        /// </summary>
        /// <param name="writer">Writer to wrap/</param>
        /// <param name="signingCredentials">SigningCredentials to be used to generate the signature.</param>
        /// <param name="referenceId">The reference Id of the envelope.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="writer"/> is null.</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="signingCredentials"/> is null.</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="referenceId"/> is null or Empty.</exception>
        public EnvelopedSignatureWriterWithReferenceIdFix(XmlWriter writer, SigningCredentials signingCredentials, string referenceId)
            : this(writer, signingCredentials, referenceId, null)
        {
        }

        internal static string[] TokenizeInclusiveNamespacesPrefixList(string inclusiveNamespacesPrefixList)
        {
            if (inclusiveNamespacesPrefixList == null)
                return null;

            string[] prefixes = inclusiveNamespacesPrefixList.Split(null);
            int count = 0;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (prefix == "#default")
                {
                    prefixes[count++] = string.Empty;
                }
                else if (prefix.Length > 0)
                {
                    prefixes[count++] = prefix;
                }
            }
            if (count == 0)
            {
                return null;
            }
            else if (count == prefixes.Length)
            {
                return prefixes;
            }
            else
            {
                string[] result = new string[count];
                Array.Copy(prefixes, result, count);
                return result;
            }
        }

        /// <summary>
        /// Initializes an instance of <see cref="EnvelopedSignatureWriter"/>. The returned writer can be directly used
        /// to write the envelope. The signature will be automatically generated when
        /// the envelope is completed.
        /// </summary>
        /// <param name="writer">Writer to wrap/</param>
        /// <param name="signingCredentials">SigningCredentials to be used to generate the signature.</param>
        /// <param name="referenceId">The reference Id of the envelope.</param>
        /// <param name="inclusivePrefixList">inclusive prefix list to use for exclusive canonicalization.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="writer"/> is null.</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="signingCredentials"/> is null.</exception>
        /// <exception cref="ArgumentNullException">if <paramref name="referenceId"/> is null or Empty.</exception>
        public EnvelopedSignatureWriterWithReferenceIdFix(XmlWriter writer, SigningCredentials signingCredentials, string referenceId, string inclusivePrefixList)
        {
            _originalWriter = writer ?? throw LogArgumentNullException(nameof(writer));
            _signingCredentials = signingCredentials ?? throw LogArgumentNullException(nameof(signingCredentials));
            if (string.IsNullOrEmpty(referenceId))
                throw LogArgumentNullException(nameof(referenceId));

            _inclusiveNamespacesPrefixList = inclusivePrefixList;
            _referenceId = referenceId;
            _writerStream = new MemoryStream();
            _canonicalStream = new MemoryStream();
            InnerWriter = CreateTextWriter(_writerStream, Encoding.UTF8, false);
            InnerWriter.StartCanonicalization(_canonicalStream, false,
                TokenizeInclusiveNamespacesPrefixList(_inclusiveNamespacesPrefixList));
            _signaturePosition = -1;
        }


        /// <summary>
        /// Gets or sets the <see cref="DSigSerializer"/> to use.
        /// </summary>
        /// <exception cref="ArgumentNullException">if value is null.</exception>
        public DSigSerializer DSigSerializer
        {
            get => _dsigSerializer;
            set => _dsigSerializer = value ?? throw LogArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Calculates and inserts the Signature.
        /// </summary>
        private void OnEndRootElement()
        {
            if (_signaturePosition == -1)
                WriteSignature();

            InnerWriter.WriteEndElement();
            InnerWriter.Flush();
            InnerWriter.EndCanonicalization();

            var signature = CreateSignature();
            var signatureStream = new MemoryStream();
            var signatureWriter = CreateTextWriter(signatureStream);
            DSigSerializer.WriteSignature(signatureWriter, signature);
            signatureWriter.Flush();
            var signatureBytes = signatureStream.ToArray();
            var writerBytes = _writerStream.ToArray();
            byte[] effectiveBytes = new byte[signatureBytes.Length + writerBytes.Length];
            Array.Copy(writerBytes, effectiveBytes, (int)_signaturePosition);
            Array.Copy(signatureBytes, 0, effectiveBytes, (int)_signaturePosition, signatureBytes.Length);
            Array.Copy(writerBytes, (int)_signaturePosition, effectiveBytes, (int)_signaturePosition + signatureBytes.Length, writerBytes.Length - (int)_signaturePosition);

            XmlReader reader = XmlDictionaryReader.CreateTextReader(effectiveBytes, XmlDictionaryReaderQuotas.Max);
            reader.MoveToContent();
            _originalWriter.WriteNode(reader, false);
            _originalWriter.Flush();
        }

        private Signature CreateSignature()
        {
            CryptoProviderFactory cryptoProviderFactory = _signingCredentials.CryptoProviderFactory ?? _signingCredentials.Key.CryptoProviderFactory;
            var hashAlgorithm = cryptoProviderFactory.CreateHashAlgorithm(_signingCredentials.Digest);
            if (hashAlgorithm == null)
                throw LogExceptionMessage(new XmlValidationException(FormatInvariant(LogMessages.IDX30213, cryptoProviderFactory.ToString(), _signingCredentials.Digest)));

            Reference reference = null;
            try
            {
                reference = new Reference(new EnvelopedSignatureTransform(), new ExclusiveCanonicalizationTransform { InclusiveNamespacesPrefixList = _inclusiveNamespacesPrefixList })
                {
                    Uri = "#" + _referenceId,
                    DigestValue = Convert.ToBase64String(hashAlgorithm.ComputeHash(_canonicalStream.ToArray())),
                    DigestMethod = _signingCredentials.Digest
                };
            }
            finally
            {
                if (hashAlgorithm != null)
                    cryptoProviderFactory.ReleaseHashAlgorithm(hashAlgorithm);
            }

            var signedInfo = new SignedInfo(reference)
            {
                CanonicalizationMethod = SecurityAlgorithms.ExclusiveC14n,
                SignatureMethod = _signingCredentials.Algorithm
            };

            var canonicalSignedInfoStream = new MemoryStream();
            var signedInfoWriter = CreateTextWriter(Stream.Null);
            signedInfoWriter.StartCanonicalization(canonicalSignedInfoStream, false, null);
            DSigSerializer.WriteSignedInfo(signedInfoWriter, signedInfo);
            signedInfoWriter.EndCanonicalization();
            signedInfoWriter.Flush();

            var provider = cryptoProviderFactory.CreateForSigning(_signingCredentials.Key, _signingCredentials.Algorithm);
            if (provider == null)
                throw LogExceptionMessage(new XmlValidationException(FormatInvariant(LogMessages.IDX30213, cryptoProviderFactory.ToString(), _signingCredentials.Key.ToString(), _signingCredentials.Algorithm)));

            try
            {
                return new Signature
                {
                    KeyInfo = new KeyInfo(_signingCredentials.Key),
                    SignatureValue = Convert.ToBase64String(provider.Sign(canonicalSignedInfoStream.ToArray())),
                    SignedInfo = signedInfo,
                };
            }
            finally
            {
                if (provider != null)
                    cryptoProviderFactory.ReleaseSignatureProvider(provider);
            }
        }

        /// <summary>
        /// Sets the position of the signature within the envelope. Call this
        /// method while writing the envelope to indicate at which point the 
        /// signature should be inserted.
        /// </summary>
        public void WriteSignature()
        {
            InnerWriter.Flush();
            _signaturePosition = _writerStream.Length;
        }

        /// <summary>
        /// Overrides the base class implementation. When the last element of the envelope is written
        /// the signature is automatically computed over the envelope and the signature is inserted at
        /// the appropriate position, if WriteSignature was explicitly called or is inserted at the
        /// end of the envelope.
        /// </summary>
        public override void WriteEndElement()
        {
            _elementCount--;
            if (_elementCount == 0)
            {
                base.Flush();
                OnEndRootElement();
            }
            else
            {
                base.WriteEndElement();
            }
        }

        /// <summary>
        /// Overrides the base class implementation. When the last element of the envelope is written
        /// the signature is automatically computed over the envelope and the signature is inserted at
        /// the appropriate position, if WriteSignature was explicitly called or is inserted at the
        /// end of the envelope.
        /// </summary>
        public override void WriteFullEndElement()
        {
            _elementCount--;
            if (_elementCount == 0)
            {
                base.Flush();
                OnEndRootElement();
            }
            else
            {
                base.WriteFullEndElement();
            }
        }

        /// <summary>
        /// Overrides the base class. Writes the specified start tag and associates
        /// it with the given namespace.
        /// </summary>
        /// <param name="prefix">The namespace prefix of the element.</param>
        /// <param name="localName">The local name of the element.</param>
        /// <param name="namespace">The namespace URI to associate with the element.</param>
        public override void WriteStartElement(string prefix, string localName, string @namespace)
        {
            _elementCount++;
            base.WriteStartElement(prefix, localName, @namespace);
        }

        #region IDisposable Members

        /// <summary>
        /// Releases the unmanaged resources used by the System.IdentityModel.Protocols.XmlSignature.EnvelopedSignatureWriter and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// True to release both managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (disposing)
            {
                if (_writerStream != null)
                {
                    _writerStream.Dispose();
                    _writerStream = null;
                }

                if (_canonicalStream != null)
                {
                    _canonicalStream.Dispose();
                    _canonicalStream = null;
                }
            }
        }

        #endregion
    }
}
