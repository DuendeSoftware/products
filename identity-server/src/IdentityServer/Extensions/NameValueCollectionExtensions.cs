// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Text;
using System.Text.Encodings.Web;

namespace Duende.IdentityServer.Extensions;

internal static class NameValueCollectionExtensions
{
    public static IDictionary<string, string[]> ToFullDictionary(this NameValueCollection source)
    {
        return source.AllKeys.ToDictionary(k => k, k => source.GetValues(k));
    }

    public static NameValueCollection FromFullDictionary(this IDictionary<string, string[]> source)
    {
        var nvc = new NameValueCollection();

        foreach (var (key, strings) in source)
        {
            foreach (var value in strings)
            {
                nvc.Add(key, value);
            }
        }

        return nvc;
    }

    public static string ToQueryString(this NameValueCollection collection)
    {
        if (collection.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(128);
        var first = true;
        foreach (string name in collection)
        {
            var values = collection.GetValues(name);
            if (values == null || values.Length == 0)
            {
                first = AppendNameValuePair(builder, first, true, name, string.Empty);
            }
            else
            {
                foreach (var value in values)
                {
                    first = AppendNameValuePair(builder, first, true, name, value);
                }
            }
        }

        return builder.ToString();
    }

    public static string ToFormPost(this NameValueCollection collection)
    {
        var builder = new StringBuilder(128);
        const string inputFieldFormat = "<input type='hidden' name='{0}' value='{1}' />\n";

        foreach (string name in collection)
        {
            var values = collection.GetValues(name);
            var value = values.First();
            value = HtmlEncoder.Default.Encode(value);
            builder.AppendFormat(inputFieldFormat, name, value);
        }

        return builder.ToString();
    }

    public static NameValueCollection ToNameValueCollection(this Dictionary<string, string> data)
    {
        var result = new NameValueCollection();

        if (data == null || data.Count == 0)
        {
            return result;
        }

        foreach (var name in data.Keys)
        {
            var value = data[name];
            if (value != null)
            {
                result.Add(name, value);
            }
        }

        return result;
    }

    public static Dictionary<string, string> ToDictionary(this NameValueCollection collection)
    {
        return collection.ToScrubbedDictionary();
    }

    public static Dictionary<string, string> ToScrubbedDictionary(this NameValueCollection collection, params string[] nameFilter)
    {
        var dict = new Dictionary<string, string>();

        if (collection == null || collection.Count == 0)
        {
            return dict;
        }

        foreach (string name in collection)
        {
            var value = collection.Get(name);
            if (value != null)
            {
                if (nameFilter.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    value = "***REDACTED***";
                }
                dict.Add(name, value);
            }
        }

        return dict;
    }

    internal static string ConvertFormUrlEncodedSpacesToUrlEncodedSpaces(string str)
    {
        if ((str != null) && (str.IndexOf('+') >= 0))
        {
            str = str.Replace("+", "%20");
        }
        return str;
    }

    private static bool AppendNameValuePair(StringBuilder builder, bool first, bool urlEncode, string name, string value)
    {
        var effectiveName = name ?? string.Empty;
        var encodedName = urlEncode ? UrlEncoder.Default.Encode(effectiveName) : effectiveName;

        var effectiveValue = value ?? string.Empty;
        var encodedValue = urlEncode ? UrlEncoder.Default.Encode(effectiveValue) : effectiveValue;
        encodedValue = ConvertFormUrlEncodedSpacesToUrlEncodedSpaces(encodedValue);

        if (first)
        {
            first = false;
        }
        else
        {
            builder.Append('&');
        }

        builder.Append(encodedName);
        if (!string.IsNullOrEmpty(encodedValue))
        {
            builder.Append('=');
            builder.Append(encodedValue);
        }
        return first;
    }
}
