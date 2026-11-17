// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// Visible only in Duende.IdentityServer. This file names every Storage query/result type
// that has a public clone in Duende.IdentityServer.Models, so code in this assembly never
// mixes the two up. Refer to Storage types only through these aliases (or StorageQuerying/
// StorageResults for generics), not through "using Duende.Storage.Internal.Querying;" etc.
global using StorageQueryRequest = Duende.Storage.Internal.Querying.QueryRequest;
global using StorageFilterBy = Duende.Storage.Internal.Querying.FilterBy;
global using StorageSortBy = Duende.Storage.Internal.Querying.SortBy;
global using StorageSearchExpression = Duende.Storage.Internal.Querying.SearchExpression;
global using StorageDataRange = Duende.Storage.Internal.Pagination.DataRange;
global using StorageDataVersion = Duende.Storage.Internal.DataVersion;
global using StorageErrorResult = Duende.Storage.Internal.ErrorResult;

// C# has no alias for an open generic type, or for a static factory type used only via its
// nested members (SaveResult<TId>, GetResult<T>). Generic/static-only usages go through these
// namespace aliases instead.
global using StorageQuerying = Duende.Storage.Internal.Querying;
global using StorageResults = Duende.Storage.Internal;
global using StoragePagination = Duende.Storage.Internal.Pagination;

// Sort order used when building a query against a repository.
global using StorageSortDirection = Duende.Storage.Internal.Querying.SortDirection;

// Named because the namespace Duende.IdentityServer.Hosting.OutboxProcessor shadows this type.
global using StorageOutboxProcessor = Duende.Storage.Internal.Outbox.OutboxProcessor;
