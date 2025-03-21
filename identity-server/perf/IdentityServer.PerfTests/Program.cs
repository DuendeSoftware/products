// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using BenchmarkDotNet.Running;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args);
