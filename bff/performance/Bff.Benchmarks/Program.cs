// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using BenchmarkDotNet.Running;


public class Program
{
    static void Main(string[] args) => BenchmarkRunner.Run(typeof(Program).Assembly);
}
