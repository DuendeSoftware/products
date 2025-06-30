// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;
using Duende.Bff.DynamicFrontends.Internal;

namespace Duende.Bff.Tests.MultiFrontend;

public class PathMapperTests
{

    [Fact]
    public void MapPath_WithNoMatchingPaths_DoesNotModifyPathBaseOrPath()
    {
        // Arrange
        var context = CreateHttpContext("/app", "/api/values");
        var frontend = CreateFrontendWithPath();

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Request.PathBase.Value.ShouldBe("/app");
        context.Request.Path.Value.ShouldBe("/api/values");
    }

    [Fact]
    public void MapPath_WithMatchingPath_UpdatesPathBaseAndPath()
    {
        // Arrange
        var context = CreateHttpContext("/app", "/api/values");
        var frontend = CreateFrontendWithPath("/api");

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Request.PathBase.Value.ShouldBe("/app/api");
        context.Request.Path.Value.ShouldBe("/values");
    }

    [Fact]
    public void MapPath_WithExactMatchingPath_UpdatesPathBaseAndLeavesEmptyPath()
    {
        // Arrange
        var context = CreateHttpContext("/app", "/api");
        var frontend = CreateFrontendWithPath("/api");

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Request.PathBase.Value.ShouldBe("/app/api");
        context.Request.Path.Value.ShouldBe("");
    }

    [Fact]
    public void MapPath_WithNoMatchingPath_Returns404()
    {
        // Arrange
        var context = CreateHttpContext("/app", "/admin/values");
        var frontend = CreateFrontendWithPath("/api");

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public void MapPath_WithEmptyPathBase_AddsMatchingPathToPathBase()
    {
        // Arrange
        var context = CreateHttpContext("", "/api/values");
        var frontend = CreateFrontendWithPath("/api");

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Request.PathBase.Value.ShouldBe("/api");
        context.Request.Path.Value.ShouldBe("/values");
    }

    [Fact]
    public void MapPath_WithCaseInsensitiveMatch_UpdatesPathBaseAndPath()
    {
        // Arrange
        var context = CreateHttpContext("/app", "/API/values");
        var frontend = CreateFrontendWithPath("/api");

        // Act
        PathMapper.MapPath(context, frontend);

        // Assert
        context.Request.PathBase.Value.ShouldBe("/app/api");
        context.Request.Path.Value.ShouldBe("/values");
    }

    // Helper methods
    private static HttpContext CreateHttpContext(string pathBase, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = new PathString(pathBase);
        context.Request.Path = new PathString(path);
        return context;
    }

    private static BffFrontend CreateFrontendWithPath(string? path = null) => new BffFrontend
    {
        Name = BffFrontendName.Parse("test-frontend"),
        SelectionCriteria = new FrontendSelectionCriteria
        {
            MatchingPath = path
        }
    };
}
