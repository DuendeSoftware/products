// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using AngleSharp.Html.Dom;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>An input in an HTML form.</summary>
internal sealed record HtmlInput(string Name, string Type, string Value);

/// <summary>An HTML form, parsed with AngleSharp: what the browser needs to fill in and submit it.</summary>
internal sealed record HtmlForm(string? Action, string Method, IReadOnlyList<HtmlInput> Inputs)
{
    public static HtmlForm From(IHtmlFormElement form) =>
        new(
            // The raw attribute: the browser resolves it against the page's URL, which AngleSharp doesn't know.
            form.GetAttribute("action"),
            form.Method.ToUpperInvariant(),
            form.Elements
                .OfType<IHtmlInputElement>()
                .Where(input => !string.IsNullOrEmpty(input.Name))
                .Select(input => new HtmlInput(input.Name!, input.Type, input.Value))
                .ToList());
}
