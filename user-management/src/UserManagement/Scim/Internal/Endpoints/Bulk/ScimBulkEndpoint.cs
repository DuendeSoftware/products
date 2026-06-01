// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using Duende.UserManagement.Scim.Internal.Endpoints.Groups;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ScimBulkEndpoint(
    IServiceProvider serviceProvider,
    IOptions<ScimOptions> scimOptions,
    ILogger<ScimBulkEndpoint> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal async Task<IResult> HandleAsync(
        ScimBulkRequest? body,
        HttpContext context,
        CancellationToken ct)
    {
        if (body is null)
        {
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidSyntax, "Request body is required.");
        }

        // Validate schemas
        if (body.Schemas is not null &&
            !body.Schemas.Contains(ScimConstants.BulkRequestSchemaUrn, StringComparer.OrdinalIgnoreCase))
        {
            return ScimResults.Error(400, ScimConstants.ErrorTypes.InvalidSyntax,
                $"Schemas must include '{ScimConstants.BulkRequestSchemaUrn}'.");
        }

        var options = scimOptions.Value;

        // Enforce maxPayloadSize: check Content-Length header against the
        // configured limit. This rejects oversized requests before processing
        // any operations.
        var contentLength = context.Request.ContentLength;
        if (contentLength.HasValue && contentLength.Value > options.MaxBulkPayloadSize)
        {
            return ScimResults.Error(413,
                $"The size of the bulk operation exceeds the maxPayloadSize ({options.MaxBulkPayloadSize}).");
        }

        // Enforce maxOperations
        if (body.Operations.Count > options.MaxBulkOperations)
        {
            return ScimResults.Error(413,
                $"The number of operations ({body.Operations.Count}) exceeds the maxOperations limit ({options.MaxBulkOperations}).");
        }

        var results = new List<ScimBulkOperationResponse>(body.Operations.Count);
        var errorCount = 0;
        var operationIndex = 0;
        var thresholdForLoggingReached = false;
        var resolver = new BulkIdResolver();

        foreach (var op in body.Operations)
        {
            // If failOnErrors threshold is reached, mark remaining ops as skipped
            if (body.FailOnErrors.HasValue && errorCount >= body.FailOnErrors.Value)
            {
                if (!thresholdForLoggingReached)
                {
                    logger.ScimBulkFailOnErrorsThresholdHit(LogLevel.Information, errorCount);
                    thresholdForLoggingReached = true;
                }

                results.Add(new ScimBulkOperationResponse
                {
                    Method = op.Method,
                    BulkId = op.BulkId,
                    Status = "skipped"
                });
                operationIndex++;
                continue;
            }

            logger.ScimBulkOperationRouting(LogLevel.Debug, operationIndex, op.Method, op.Path);
            var opResult = await ProcessOperationAsync(op, resolver, ct);
            results.Add(opResult);

            var statusCode = int.TryParse(opResult.Status, out var code) ? code : 0;
            if (statusCode >= 400)
            {
                logger.ScimBulkOperationErrorStatus(LogLevel.Warning, operationIndex);
                errorCount++;
            }

            operationIndex++;
        }

        logger.ScimBulkCompleted(LogLevel.Information, body.Operations.Count, errorCount);
        return ScimResults.Ok(new ScimBulkResponse { Operations = results });
    }

    private async Task<ScimBulkOperationResponse> ProcessOperationAsync(
        ScimBulkOperation op,
        BulkIdResolver resolver,
        CancellationToken ct)
    {
        // Parse and validate the path + method combination
        var route = BulkOperationRouter.Parse(op.Method, op.Path);
        if (!route.IsValid)
        {
            return ErrorResponse(op, 400, ScimConstants.ErrorTypes.InvalidValue, route.ErrorDetail!);
        }

        // Resolve bulkId reference in the resource ID (for PUT/PATCH/DELETE)
        var resourceId = route.ResourceId;
        if (resourceId is not null)
        {
            if (!resolver.TryResolvePath(ref resourceId))
            {
                return ErrorResponse(op, 409, ScimConstants.ErrorTypes.Uniqueness,
                    $"Cannot resolve bulkId reference '{resourceId}': the referenced operation has not been processed yet.");
            }
        }

        // Resolve bulkId references in the data body text (if present)
        string? resolvedDataJson = null;
        if (op.Data.HasValue)
        {
            var rawJson = op.Data.Value.GetRawText();
            resolvedDataJson = resolver.ResolveJsonText(rawJson);
            if (resolvedDataJson is null)
            {
                return ErrorResponse(op, 409, ScimConstants.ErrorTypes.Uniqueness,
                    "Cannot resolve one or more bulkId references in the request data.");
            }
        }

        // Execute the operation via shared processors
        var result = await ExecuteOperationAsync(op, route, resourceId, resolvedDataJson, ct);
        var statusCode = result.StatusCode;
        var location = result.Location;
        var etag = result.ETag;
        var errorBody = result.IsError ? BuildErrorBody(result.StatusCode, result.ScimType, result.Detail ?? "Request failed.") : null;

        // For successful POST operations, register the new resource ID with the resolver
        if (op.Method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase) &&
            statusCode == 201 &&
            op.BulkId is not null &&
            location is not null)
        {
            var createdId = ExtractIdFromLocation(location);
            if (createdId is not null)
            {
                resolver.Register(op.BulkId, createdId);
                logger.ScimBulkIdResolved(LogLevel.Debug, op.BulkId, createdId);
            }
        }

        if (statusCode >= 400)
        {
            return new ScimBulkOperationResponse
            {
                Method = op.Method,
                BulkId = op.BulkId,
                Location = location,
                Status = statusCode.ToString(CultureInfo.InvariantCulture),
                Response = errorBody
            };
        }

        return new ScimBulkOperationResponse
        {
            Method = op.Method,
            BulkId = op.BulkId,
            Version = etag,
            Location = location,
            Status = statusCode.ToString(CultureInfo.InvariantCulture)
        };
    }

    private async Task<ScimOperationResult> ExecuteOperationAsync(
        ScimBulkOperation op,
        BulkRouteResult route,
        string? resourceId,
        string? resolvedDataJson,
        CancellationToken ct)
    {
        try
        {
            return await DispatchAsync(op, route, resourceId, resolvedDataJson, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Per-operation isolation: catch all non-cancellation exceptions
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.ScimBulkOperationError(LogLevel.Error, op.Method, op.Path, ex);
            return ScimOperationResult.Error(500, null, "An unexpected error occurred.");
        }
    }

    private async Task<ScimOperationResult> DispatchAsync(
        ScimBulkOperation op,
        BulkRouteResult route,
        string? resourceId,
        string? resolvedDataJson,
        CancellationToken ct)
    {
        var method = op.Method;
        var isUser = route.ResourceType == ScimConstants.ResourceTypes.User;

        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        if (method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase) && isUser)
        {
            return await sp.GetRequiredService<ScimUserCommandProcessor>()
                .CreateAsync(DeserializeData<ScimUserRequest>(resolvedDataJson), ct);
        }

        if (method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase) && isUser)
        {
            return await sp.GetRequiredService<ScimUserCommandProcessor>()
                .ReplaceAsync(resourceId!, DeserializeData<ScimUserRequest>(resolvedDataJson), op.Version, ct);
        }

        if (method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase) && isUser)
        {
            return await sp.GetRequiredService<ScimUserCommandProcessor>()
                .PatchAsync(resourceId!, DeserializeData<ScimPatchRequest>(resolvedDataJson), op.Version, ct);
        }

        if (method.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase) && isUser)
        {
            return await sp.GetRequiredService<ScimUserCommandProcessor>()
                .DeleteAsync(resourceId!, op.Version, ct);
        }

        if (method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase) && !isUser)
        {
            return await sp.GetRequiredService<ScimGroupCommandProcessor>()
                .CreateAsync(DeserializeData<ScimGroupRequest>(resolvedDataJson), ct);
        }

        if (method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase) && !isUser)
        {
            return await sp.GetRequiredService<ScimGroupCommandProcessor>()
                .ReplaceAsync(resourceId!, DeserializeData<ScimGroupRequest>(resolvedDataJson), op.Version, ct);
        }

        if (method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase) && !isUser)
        {
            return await sp.GetRequiredService<ScimGroupCommandProcessor>()
                .PatchAsync(resourceId!, DeserializeData<ScimPatchRequest>(resolvedDataJson), op.Version, ct);
        }

        if (method.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase) && !isUser)
        {
            return await sp.GetRequiredService<ScimGroupCommandProcessor>()
                .DeleteAsync(resourceId!, op.Version, ct);
        }

        return ScimOperationResult.Error(405,
            $"Method '{op.Method}' is not supported for bulk operations.");
    }

    private static T? DeserializeData<T>(string? json) =>
        json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static ScimBulkOperationResponse ErrorResponse(
        ScimBulkOperation op,
        int statusCode,
        string? scimType,
        string detail) =>
        new()
        {
            Method = op.Method,
            BulkId = op.BulkId,
            Status = statusCode.ToString(CultureInfo.InvariantCulture),
            Response = BuildErrorBody(statusCode, scimType, detail)
        };

    private static ScimErrorResponse BuildErrorBody(int statusCode, string? scimType, string detail) =>
        new()
        {
            Status = statusCode.ToString(CultureInfo.InvariantCulture),
            ScimType = scimType,
            Detail = detail
        };

    private static string? ExtractIdFromLocation(string location)
    {
        var lastSlash = location.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < location.Length - 1
            ? location[(lastSlash + 1)..]
            : null;
    }
}
