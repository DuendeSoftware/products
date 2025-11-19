// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Running;
using Bff.Benchmarks;


BenchmarkRunner.Run(typeof(Program).Assembly, new BenchmarkConfig());
