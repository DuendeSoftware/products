// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using FluentAssertions;

namespace UnitTests.Extensions;

public class ApiResourceSigningAlgorithmSelectionTests
{
    [Fact]
    public void Single_resource_no_allowed_algorithms_set_should_return_empty_list()
    {
        var resource = new ApiResource();

        var allowedAlgorithms = new List<ApiResource> { resource }.FindMatchingSigningAlgorithms();

        allowedAlgorithms.Count.Should().Be(0);
    }
        
    [Fact]
    public void Two_resources_no_allowed_algorithms_set_should_return_empty_list()
    {
        var resource1 = new ApiResource();
        var resource2 = new ApiResource();

        var allowedAlgorithms = new List<ApiResource> { resource1, resource2 }.FindMatchingSigningAlgorithms();

        allowedAlgorithms.Count.Should().Be(0);
    }
        
    [Theory]
    [InlineData(new [] { "A" }, new [] { "A" }, 
        new [] { "A" })]
    [InlineData(new [] { "A", "B" }, new [] { "A", "B" }, 
        new [] { "A", "B" })]
    [InlineData(new [] { "A", "B", "C" }, new [] { "A", "B", "C" }, 
        new [] { "A", "B", "C" })]

    [InlineData(new [] { "A", "B" }, new [] { "A", "D" }, 
        new [] { "A" })]
    [InlineData(new [] { "A", "B", "C" }, new [] { "A", "B", "Z" }, 
        new [] { "A", "B" })]

    [InlineData(new string[] { }, new [] { "B" },
        new string[] { "B" })]
    [InlineData(new string[] { }, new [] { "C", "D" },
        new string[] { "C", "D" })]

    [InlineData(new [] { "A" }, new [] { "B" }, 
        new string[] { })]
    [InlineData(new [] { "A", "B" }, new [] { "C", "D" }, 
        new string[] { })]
    public void Two_resources_with_allowed_algorithms_set_should_return_right_values(
        string[] resource1Algorithms, string[] resource2Algorithms, 
        string[] expectedAlgorithms)
    {
        var resource1 = new ApiResource()
        {
            AllowedAccessTokenSigningAlgorithms = resource1Algorithms
        };
            
        var resource2 = new ApiResource
        {
            AllowedAccessTokenSigningAlgorithms = resource2Algorithms
        };

        if (expectedAlgorithms.Any())
        {
            var allowedAlgorithms = new List<ApiResource> { resource1, resource2 }.FindMatchingSigningAlgorithms();
            allowedAlgorithms.Should().BeEquivalentTo(expectedAlgorithms);
        }
        else
        {
            Action act = () => new List<ApiResource> { resource1, resource2 }.FindMatchingSigningAlgorithms();
            act.Should().Throw<InvalidOperationException>();
        }
    }
        
    [Theory]
    [InlineData(new [] { "A" }, new [] { "A" }, new [] { "A" }, 
        new [] { "A" })]
    [InlineData(new [] { "A", "B" }, new [] { "A", "B" }, new [] { "A", "B" }, 
        new [] { "A", "B" })]
    [InlineData(new [] { "A", "B", "C" }, new [] { "A", "B", "C" }, new [] { "A", "B", "C" }, 
        new [] { "A", "B", "C" })]
        
    [InlineData(new [] { "A", "B" }, new [] { "A", "D" }, new [] { "A", "E" } ,
        new [] { "A" })]
    [InlineData(new [] { "A", "B", "X" }, new [] { "A", "B", "Y" }, new [] { "A", "B", "Z" },
        new [] { "A", "B" })]
    [InlineData(new [] { "A", "B", "X" }, new [] { "C", "D", "X" }, new [] { "E", "F", "X" },
        new [] { "X" })]

    [InlineData(new[] { "A", "B" }, new[] { "A", "D" }, new string[] { },
        new[] { "A" })]
    [InlineData(new[] { "A", "B" }, new[] { "A", "C", "B" }, new string[] { },
        new[] { "A", "B" })]
    [InlineData(new[] { "A", "B" }, new string[] { }, new string[] { },
        new[] { "A", "B" })]

    [InlineData(new [] { "A" }, new [] { "B" }, new [] { "C" }, 
        new string[] { })]
    [InlineData(new [] { "A", "B" }, new [] { "C", "D" }, new [] { "X", "Y" }, 
        new string[] { })]
    [InlineData(new [] { "A", "B", "C" }, new [] { "C", "D", "E" }, new [] { "E", "F", "G" },
        new string[] { })]
    public void Three_resources_with_allowed_algorithms_set_should_return_right_values(
        string[] resource1Algorithms, string[] resource2Algorithms, string[] resource3Algorithms, 
        string[] expectedAlgorithms)
    {
        var resource1 = new ApiResource()
        {
            AllowedAccessTokenSigningAlgorithms = resource1Algorithms
        };
            
        var resource2 = new ApiResource
        {
            AllowedAccessTokenSigningAlgorithms = resource2Algorithms
        };
            
        var resource3 = new ApiResource
        {
            AllowedAccessTokenSigningAlgorithms = resource3Algorithms
        };

        if (expectedAlgorithms.Any())
        {
            var allowedAlgorithms = new List<ApiResource> {resource1, resource2, resource3}.FindMatchingSigningAlgorithms();
            allowedAlgorithms.Should().BeEquivalentTo(expectedAlgorithms);
        }
        else
        {
            Action act = () => new List<ApiResource> {resource1, resource2, resource3}.FindMatchingSigningAlgorithms();
            act.Should().Throw<InvalidOperationException>();
        }
    }
}