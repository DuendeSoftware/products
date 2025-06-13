// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;


BenchmarkRunner.Run(typeof(Program).Assembly, ManualConfig.CreateMinimumViable());
