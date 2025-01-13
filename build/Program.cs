// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.IO;
using System.Threading.Tasks;

using static Bullseye.Targets;
using static SimpleExec.Command;

namespace build
{
    internal static class Program
    {
        private const string solution = "Duende.IdentityServer.sln";
        private const string solutionCodeQL = "Duende.IdentityServer.CodeQL.sln";
        private const string packOutput = "./artifacts";
        private const string envVarMissing = " environment variable is missing. Aborting.";

        private static class Targets
        {
            public const string RestoreTools = "restore-tools";
            public const string CleanBuildOutput = "clean-build-output";
            public const string CleanPackOutput = "clean-pack-output";
            public const string Build = "build";
            public const string CodeQL = "codeql";
            public const string Test = "test";
            public const string Pack = "pack";
            public const string SignPackage = "sign-package";
        }

        static async Task Main(string[] args)
        {
            Target(Targets.RestoreTools, () =>
            {
                Run("dotnet", "tool restore");
            });

            Target(Targets.CleanBuildOutput, () =>
            {
                Run("dotnet", $"clean {solution} -c Release -v m --nologo");
            });

            Target(Targets.Build, DependsOn(Targets.CleanBuildOutput), () =>
            {
                Run("dotnet", $"build {solution} -c Release --nologo");
            });
            
            Target(Targets.CodeQL, () =>
            {
                Run("dotnet", $"build {solutionCodeQL} -c Release --nologo");
            });

            Target(Targets.Test, DependsOn(Targets.Build), () =>
            {
                Run("dotnet", $"test {solution} -c Release --no-build --nologo");
            });

            Target(Targets.CleanPackOutput, () =>
            {
                if (Directory.Exists(packOutput))
                {
                    Directory.Delete(packOutput, true);
                }
            });

            Target(Targets.Pack, DependsOn(Targets.Build, Targets.CleanPackOutput), () =>
            {
                var directory = Directory.CreateDirectory(packOutput).FullName;
                
                Run("dotnet", $"pack ./src/Storage/Duende.IdentityServer.Storage.csproj -c Release -o {directory} --no-build --nologo");
                Run("dotnet", $"pack ./src/IdentityServer/Duende.IdentityServer.csproj -c Release -o {directory} --no-build --nologo");
                
                Run("dotnet", $"pack ./src/EntityFramework.Storage/Duende.IdentityServer.EntityFramework.Storage.csproj -c Release -o {directory} --no-build --nologo");
                Run("dotnet", $"pack ./src/EntityFramework/Duende.IdentityServer.EntityFramework.csproj -c Release -o {directory} --no-build --nologo");
                
                Run("dotnet", $"pack ./src/Configuration/Duende.IdentityServer.Configuration.csproj -c Release -o {directory} --no-build --nologo");
                Run("dotnet", $"pack ./src/Configuration.EntityFramework/Duende.IdentityServer.Configuration.EntityFramework.csproj -c Release -o {directory} --no-build --nologo");

                Run("dotnet", $"pack ./src/AspNetIdentity/Duende.IdentityServer.AspNetIdentity.csproj -c Release -o {directory} --no-build --nologo");
            });

            Target(Targets.SignPackage, DependsOn(Targets.Pack, Targets.RestoreTools), () =>
            {
                SignNuGet();
            });

            Target("default", DependsOn(Targets.Test, Targets.Pack));
            Target("sign", DependsOn(Targets.Test, Targets.SignPackage));

            await RunTargetsAndExitAsync(args, ex => ex is SimpleExec.ExitCodeException || ex.Message.EndsWith(envVarMissing));
        }

        private static void SignNuGet()
        {
            var signClientSecret = Environment.GetEnvironmentVariable("SignClientSecret");

            if (string.IsNullOrWhiteSpace(signClientSecret))
            {
                throw new Exception($"SignClientSecret{envVarMissing}");
            }

            foreach (var file in Directory.GetFiles(packOutput, "*.nupkg", SearchOption.AllDirectories))
            {
                Console.WriteLine($"  Signing {file}");

                Run("dotnet",
                        "NuGetKeyVaultSignTool " +
                        $"sign {file} " +
                        "--file-digest sha256 " +
                        "--timestamp-rfc3161 http://timestamp.digicert.com " +
                        "--azure-key-vault-url https://duendecodesigning.vault.azure.net/ " +
                        "--azure-key-vault-client-id 18e3de68-2556-4345-8076-a46fad79e474 " +
                        "--azure-key-vault-tenant-id ed3089f0-5401-4758-90eb-066124e2d907 " +
                        $"--azure-key-vault-client-secret {signClientSecret} " +
                        "--azure-key-vault-certificate CodeSigning"
                        ,noEcho: true, handleExitCode: code => false);
            }
        }
    }
}