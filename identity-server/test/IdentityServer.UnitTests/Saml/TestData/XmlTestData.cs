// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Runtime.CompilerServices;
using System.Xml;
using Duende.IdentityServer.Saml.Xml;

namespace UnitTests.Saml.TestData;

internal static class XmlTestData
{
    public static XmlTraverser GetXmlTraverser<TDirectory>([CallerMemberName] string? testName = null)
    {
        var document = GetXmlDocument<TDirectory>(testName);
        return new XmlTraverser(document?.DocumentElement
            ?? throw new InvalidOperationException($"Missing file or XmlDoc contained no DocumentElement for test '{testName}'"));
    }

    public static XmlDocument? GetXmlDocument<TDirectory>([CallerMemberName] string? testName = null)
    {
        ArgumentNullException.ThrowIfNull(testName);

        var typeName = typeof(TDirectory).FullName!;
        // Convert namespace + class name to path relative to output directory.
        // Namespace "UnitTests.Saml" + class "SamlXmlReaderXmlFileTests" → "Saml/SamlXmlReaderXmlFileTests"
        // Strip the root namespace "UnitTests." prefix.
        const string rootNamespace = "UnitTests.";
        var relativePath = typeName.StartsWith(rootNamespace)
            ? typeName[rootNamespace.Length..].Replace('.', '/')
            : typeName.Replace('.', '/');
        var fileName = Path.Combine(AppContext.BaseDirectory, relativePath, testName + ".xml");

        if (!File.Exists(fileName))
        {
            return null;
        }

        var document = new XmlDocument();
        document.Load(fileName);
        return document;
    }
}
