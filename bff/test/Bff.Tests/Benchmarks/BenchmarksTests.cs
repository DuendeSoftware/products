// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Bff.Benchmarks;
namespace Duende.Bff.Tests.Benchmarks;

public static class BenchmarkTests
{
    public class Login(Login.Fixture fixture)
        : TestBase<Login.Fixture, MultiFrontendBenchmarks>(fixture)
    {
        public class Fixture : MultiFrontendBenchmarks, IAsyncLifetime;
    }

    public class Proxy(Proxy.Fixture fixture)
        : TestBase<Proxy.Fixture, SingleFrontendBenchmarks>(fixture)
    {
        public class Fixture : SingleFrontendBenchmarks, IAsyncLifetime;
    }

    public abstract class TestBase<TFixture, TBenchmarks>(TFixture fixture)
        : IClassFixture<TFixture>
        where TFixture : TBenchmarks, IAsyncLifetime
        where TBenchmarks : BenchmarkBase

    {
        [Theory]
        [MemberData(nameof(GetBenchmarksInFixture))]
        public async Task InvokeBenchmarksAsTests(string benchmark, string testName)
        {
            var method = typeof(TFixture).GetMethod(testName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException(
                    $"No benchmark method {testName}found for benchmark {benchmark}");
            }

            await (Task)method.Invoke(fixture, null)!;
        }

        public static IEnumerable<object[]> GetBenchmarksInFixture()
        {
            var testMethods = typeof(TBenchmarks)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.GetCustomAttribute<BenchmarkDotNet.Attributes.BenchmarkAttribute>() != null)
                .Select(x => x.Name); // Excludes property getters/setters

            return testMethods.Select(x => new[] { typeof(TBenchmarks).Name, x }).ToArray();
        }
    }
}
