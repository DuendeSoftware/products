// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Marker service indicating that SCIM has been enabled via <c>EnableScim()</c>.
/// Used by <c>MapScim()</c> to guard against misconfiguration.
/// </summary>
internal sealed class ScimEnabledMarker;

internal static class ScimConstants
{
    // Authentication and authorization
    internal const string AuthenticationScheme = "DuendeScimBearer";
    internal const string WritePolicyName = "DuendeScimWrite";
    internal const string ReadPolicyName = "DuendeScimRead";

    // Schema URNs — core resources
    internal const string UserSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:User";
    internal const string GroupSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Group";

    // Schema URNs — metadata/discovery (RFC 7643 §7-8, RFC 7644 §4)
    internal const string ServiceProviderConfigSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig";
    internal const string ResourceTypeSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:ResourceType";
    internal const string SchemaSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Schema";

    // Schema URNs — protocol messages
    internal const string ListResponseSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    internal const string ErrorSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:Error";
    internal const string PatchOpSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    internal const string SearchRequestSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:SearchRequest";
    internal const string BulkRequestSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:BulkRequest";
    internal const string BulkResponseSchemaUrn = "urn:ietf:params:scim:api:messages:2.0:BulkResponse";
    internal const string ScimContentType = "application/scim+json";

    // SCIM attribute names — use these in [JsonPropertyName] and for attribute projection/mapping
    internal static class Attributes
    {
        internal const string Schemas = "schemas";
        internal const string Id = "id";
        internal const string ExternalId = "externalId";
        internal const string UserName = "userName";
        internal const string Meta = "meta";
        internal const string ResourceType = "resourceType";
        internal const string Location = "location";
        internal const string Version = "version";
        internal const string TotalResults = "totalResults";
        internal const string StartIndex = "startIndex";
        internal const string ItemsPerPage = "itemsPerPage";
        internal const string Resources = "Resources";
        internal const string Status = "status";
        internal const string ScimType = "scimType";
        internal const string Detail = "detail";
        internal const string Operations = "Operations";
        internal const string Op = "op";
        internal const string Path = "path";
        internal const string Value = "value";
        internal const string Filter = "filter";
        internal const string SortBy = "sortBy";
        internal const string SortOrder = "sortOrder";
        internal const string Count = "count";
        internal const string Password = "password";
        internal const string AttributesParam = "attributes";
        internal const string ExcludedAttributes = "excludedAttributes";

        // Bulk operation attributes (RFC 7644 §3.7)
        internal const string Method = "method";
        internal const string BulkId = "bulkId";
        internal const string Data = "data";
        internal const string Response = "response";
        internal const string FailOnErrors = "failOnErrors";

        // Metadata endpoint attributes
        internal const string Name = "name";
        internal const string Description = "description";
        internal const string Endpoint = "endpoint";
        internal const string Schema = "schema";
        internal const string SchemaExtensions = "schemaExtensions";
        internal const string Supported = "supported";
        internal const string Type = "type";
        internal const string MultiValued = "multiValued";
        internal const string Required = "required";
        internal const string CaseExact = "caseExact";
        internal const string Mutability = "mutability";
        internal const string Returned = "returned";
        internal const string Uniqueness = "uniqueness";
        internal const string SubAttributes = "subAttributes";
        internal const string AttributesList = "attributes";
        internal const string DocumentationUri = "documentationUri";
        internal const string SpecUri = "specUri";

        // Group resource attributes
        internal const string DisplayName = "displayName";
        internal const string Members = "members";
        internal const string Display = "display";
        internal const string Ref = "$ref";
    }

    /// <summary>
    /// Schema attribute name used to store the SCIM userName as a profile attribute.
    /// Lowercase because AttributeName only accepts lowercase names.
    /// </summary>
    internal const string UserNameAttributeName = "username";

    /// <summary>
    /// Schema attribute name used to store the SCIM externalId as a profile attribute.
    /// Lowercase because AttributeName only accepts lowercase names.
    /// </summary>
    internal const string ExternalIdAttributeName = "externalid";

    /// <summary>SCIM attribute data types (RFC 7643 §2.3).</summary>
    internal static class DataTypes
    {
        internal const string String = "string";
        internal const string Boolean = "boolean";
        internal const string Integer = "integer";
        internal const string Decimal = "decimal";
        internal const string DateTime = "dateTime";
        internal const string Complex = "complex";
        internal const string Reference = "reference";
    }

    /// <summary>SCIM attribute mutability values (RFC 7643 §2.2).</summary>
    internal static class MutabilityValues
    {
        internal const string ReadOnly = "readOnly";
        internal const string ReadWrite = "readWrite";
        internal const string Immutable = "immutable";
        internal const string WriteOnly = "writeOnly";
    }

    /// <summary>SCIM attribute returned values (RFC 7643 §2.2).</summary>
    internal static class ReturnedValues
    {
        internal const string Always = "always";
        internal const string Never = "never";
        internal const string Default = "default";
        internal const string Request = "request";
    }

    /// <summary>SCIM attribute uniqueness values (RFC 7643 §2.2).</summary>
    internal static class UniquenessValues
    {
        internal const string None = "none";
        internal const string Server = "server";
        internal const string Global = "global";
    }

    /// <summary>SCIM resource type names (RFC 7643 §6, §7, §8).</summary>
    internal static class ResourceTypes
    {
        internal const string User = "User";
        internal const string Users = "Users";
        internal const string Group = "Group";
        internal const string Groups = "Groups";
        internal const string Schema = "Schema";
        internal const string ResourceType = "ResourceType";
        internal const string ServiceProviderConfig = "ServiceProviderConfig";
    }

    internal static class PatchOps
    {
        internal const string Add = "add";
        internal const string Replace = "replace";
        internal const string Remove = "remove";
    }

    internal static class ErrorTypes
    {
        internal const string InvalidFilter = "invalidFilter";
        internal const string TooMany = "tooMany";
        internal const string Uniqueness = "uniqueness";
        internal const string Mutability = "mutability";
        internal const string InvalidSyntax = "invalidSyntax";
        internal const string InvalidPath = "invalidPath";
        internal const string NoTarget = "noTarget";
        internal const string InvalidValue = "invalidValue";
    }
}
