using System;
using System.IO;
using System.Threading.Tasks;

using static Bullseye.Targets;
using static SimpleExec.Command;

namespace build
{
    internal static class Program
    {
        private const string packOutput = "./artifacts";
        private const string envVarMissing = " environment variable is missing. Aborting.";

        private static class Targets
        {
            public const string RestoreTools = "restore-tools";
            public const string CleanBuildOutput = "clean-build-output";
            public const string CleanPackOutput = "clean-pack-output";
            public const string Build = "build";
            public const string Test = "test";
            public const string Pack = "pack";
            public const string SignBinary = "sign-binary";
            public const string SignPackage = "sign-package";
        }

        internal static async Task Main(string[] args)
        {
            Target(Targets.RestoreTools, () =>
            {
                Run("dotnet", "tool restore");
            });

            Target(Targets.CleanBuildOutput, () =>
            {
                Run("dotnet", "clean -c Release -v m --nologo");
            });

            Target(Targets.Build, DependsOn(Targets.CleanBuildOutput), () =>
            {
                Run("dotnet", "build -c Release --nologo");
            });

            Target(Targets.Test, DependsOn(Targets.Build), () =>
            {
                Run("dotnet", "test -c Release --no-build --nologo");
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
                Run("dotnet", $"pack ./src/Duende.Bff/Duende.Bff.csproj -c Release -o {Directory.CreateDirectory(packOutput).FullName} --no-build --nologo");
                Run("dotnet", $"pack ./src/Duende.Bff.EntityFramework/Duende.Bff.EntityFramework.csproj -c Release -o {Directory.CreateDirectory(packOutput).FullName} --no-build --nologo");
                Run("dotnet", $"pack ./src/Duende.Bff.Yarp/Duende.Bff.Yarp.csproj -c Release -o {Directory.CreateDirectory(packOutput).FullName} --no-build --nologo");
                Run("dotnet", $"pack ./src/Duende.Bff.Blazor/Duende.Bff.Blazor.csproj -c Release -o {Directory.CreateDirectory(packOutput).FullName} --no-build --nologo");
                Run("dotnet", $"pack ./src/Duende.Bff.Blazor.Client/Duende.Bff.Blazor.Client.csproj -c Release -o {Directory.CreateDirectory(packOutput).FullName} --no-build --nologo");
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
                        ,noEcho: true);
            }
        }
    }
}
