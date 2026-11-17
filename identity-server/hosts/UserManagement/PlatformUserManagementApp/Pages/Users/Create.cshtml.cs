// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlatformUserManagementApp.Pages.Users;

[Authorize]
public sealed partial class CreateModel(IUserProfileAdmin profileAdmin) : PageModel
{
    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> Schema { get; set; } =
        new Dictionary<AttributeCode, AttributeDefinition>();
    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; set; } =
        new Dictionary<AttributeGroupCode, AttributeGroup>();
    public List<string> ErrorMessages { get; set; } = [];

    public async Task OnGetAsync()
    {
        var schema = await profileAdmin.GetSchemaAsync(HttpContext.RequestAborted);
        Schema = schema.AttributeDefinitions;
        Groups = schema.Groups;
    }

    public async Task<IActionResult> OnPostAsync([FromForm] Dictionary<string, string> attrs)
    {
        var schema = await profileAdmin.GetSchemaAsync(HttpContext.RequestAborted);
        Schema = schema.AttributeDefinitions;
        Groups = schema.Groups;

        var attributes = new AttributeValueCollection(schema);

        // Handle scalar attributes
        foreach (var (key, value) in attrs)
        {
            if (!AttributeCode.TryCreate(key, out var attrName))
            {
                continue;
            }

            if (!Schema.TryGetValue(attrName.Value, out var definition))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (definition.DataType == ScalarDataType.Boolean)
            {
                if (bool.TryParse(value, out var boolVal))
                {
                    attributes.Set(attrName.Value, boolVal);
                }
            }
            else
            {
                attributes.Set(attrName.Value, value);
            }
        }

        // Handle list-of-complex attributes from form fields like list_emails[0].value
        var listFields = ParseListFields(Request.Form);
        foreach (var (attrNameStr, items) in listFields)
        {
            if (!AttributeCode.TryCreate(attrNameStr, out var attrName))
            {
                continue;
            }

            if (!Schema.TryGetValue(attrName.Value, out var definition))
            {
                continue;
            }

            if (definition.AttributeType is not ListAttributeType { ElementType: ComplexAttributeType complexType })
            {
                continue;
            }

            var listItems = new List<object>();
            foreach (var itemProps in items.OrderBy(kv => kv.Key))
            {
                var dict = new Dictionary<string, object>();
                foreach (var (propName, propValue) in itemProps.Value)
                {
                    if (string.IsNullOrWhiteSpace(propValue))
                    {
                        continue;
                    }

                    if (complexType.Properties.TryGetValue(propName, out var propType)
                        && propType.Type is ScalarAttributeType { DataType: ScalarDataType.Boolean })
                    {
                        if (bool.TryParse(propValue, out var bv))
                        {
                            dict[propName] = bv;
                        }
                    }
                    else
                    {
                        dict[propName] = propValue;
                    }
                }

                if (dict.Count > 0)
                {
                    listItems.Add((IReadOnlyDictionary<string, object>)dict);
                }
            }

            if (listItems.Count > 0)
            {
                attributes.Set(attrName.Value, (IReadOnlyList<object>)listItems);
            }
        }

        // Handle single complex attributes from form fields like complex_name.givenName
        var complexFields = ParseComplexFields(Request.Form);
        foreach (var (attrNameStr, props) in complexFields)
        {
            if (!AttributeCode.TryCreate(attrNameStr, out var attrName))
            {
                continue;
            }

            if (!Schema.TryGetValue(attrName.Value, out var definition))
            {
                continue;
            }

            if (definition.AttributeType is not ComplexAttributeType complexType)
            {
                continue;
            }

            var dict = new Dictionary<string, object>();
            foreach (var (propName, propValue) in props)
            {
                if (string.IsNullOrWhiteSpace(propValue))
                {
                    continue;
                }

                if (complexType.Properties.TryGetValue(propName, out var propType)
                    && propType.Type is ScalarAttributeType { DataType: ScalarDataType.Boolean })
                {
                    if (bool.TryParse(propValue, out var bv))
                    {
                        dict[propName] = bv;
                    }
                }
                else
                {
                    dict[propName] = propValue;
                }
            }

            if (dict.Count > 0)
            {
                attributes.Set(attrName.Value, (IReadOnlyDictionary<string, object>)dict);
            }
        }

        var profile = await profileAdmin.TryAddAsync(UserSubjectId.New(), attributes.Validate(), HttpContext.RequestAborted);
        if (profile is null)
        {
            ErrorMessages.Add("Failed to create user. Please try again.");
            return Page();
        }

        return RedirectToPage("/Users/Detail", new { id = profile.SubjectId.Value });
    }

    /// <summary>
    /// Parses form fields like "list_emails[0].value" into a nested structure:
    /// { "emails" => { 0 => { "value" => "..." } } }
    /// </summary>
    private static Dictionary<string, SortedDictionary<int, Dictionary<string, string>>> ParseListFields(IFormCollection form)
    {
        var result = new Dictionary<string, SortedDictionary<int, Dictionary<string, string>>>();

        foreach (var key in form.Keys)
        {
            var match = ListFieldPattern().Match(key);
            if (!match.Success)
            {
                continue;
            }

            var attrName = match.Groups[1].Value;
            var index = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var propName = match.Groups[3].Value;

            if (!result.TryGetValue(attrName, out var items))
            {
                items = new SortedDictionary<int, Dictionary<string, string>>();
                result[attrName] = items;
            }

            if (!items.TryGetValue(index, out var props))
            {
                props = new Dictionary<string, string>();
                items[index] = props;
            }

            props[propName] = form[key].ToString();
        }

        return result;
    }

    [GeneratedRegex(@"^list_(\w+)\[(\d+)\]\.(\w+)$")]
    private static partial Regex ListFieldPattern();

    /// <summary>
    /// Parses form fields like "complex_name.givenName" into:
    /// { "name" => { "givenName" => "..." } }
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>> ParseComplexFields(IFormCollection form)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        foreach (var key in form.Keys)
        {
            var match = ComplexFieldPattern().Match(key);
            if (!match.Success)
            {
                continue;
            }

            var attrName = match.Groups[1].Value;
            var propName = match.Groups[2].Value;

            if (!result.TryGetValue(attrName, out var props))
            {
                props = new Dictionary<string, string>();
                result[attrName] = props;
            }

            props[propName] = form[key].ToString();
        }

        return result;
    }

    [GeneratedRegex(@"^complex_(\w+)\.(\w+)$")]
    private static partial Regex ComplexFieldPattern();
}
