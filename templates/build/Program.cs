
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;

string[] paths = [
    "../bff/templates",
        "../identity-server/templates"
];

if (!TryFindFile("templates.csproj", out var found))
{
    Console.WriteLine("Failed to find templates.csproj");
    return -1;
}

Environment.CurrentDirectory = found.Directory!.FullName;


var artifactsDir = new DirectoryInfo(Path.GetFullPath("../artifacts"));
if (artifactsDir.Exists)
{
    artifactsDir.Delete(true);
}

artifactsDir.Create();

CopyFile(artifactsDir, new FileInfo("templates.csproj"));
CopyFile(artifactsDir, new FileInfo("README.md"));

// foreach path
foreach (var path in paths.Select(Path.GetFullPath))
{
    var source = new DirectoryInfo(path);

    CopyDir(source, artifactsDir);

}

Console.WriteLine($"Copied template files to {artifactsDir}");
Console.WriteLine("You can do a dotnet pack from there");

return 0;

void CopyDir(DirectoryInfo source, DirectoryInfo target)
{
    if (!target.Exists)
    {
        target.Create();
    }

    foreach (var file in source.EnumerateFiles())
    {
        if (file.Name == "Directory.Build.props")
        {
            continue;
        }

        CopyFile(target, file);
    }


    foreach (var child in source.GetDirectories())
    {
        if (child.Name == "obj" || child.Name == "bin")
        {
            continue;
        }

        CopyDir(child,  new DirectoryInfo(Path.Combine(target.FullName, child.Name)));
    }

}

void CopyFile(DirectoryInfo directoryInfo, FileInfo fileInfo)
{
    var destFileName = Path.Combine(directoryInfo.FullName, fileInfo.Name);
    fileInfo.CopyTo(destFileName, true);
}

bool TryFindFile(string fileName, [NotNullWhen(true)]out FileInfo? found)
{


    var currentDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
                                       ?? throw new InvalidOperationException("Failed to find directory for current assembly"));

    while (currentDir != null && currentDir.Exists)
    {
        var lookingFor = Path.Combine(currentDir.FullName, fileName);
        if (File.Exists(lookingFor))
        {
            found = new FileInfo(lookingFor);
            return true;
        }

        currentDir = currentDir.Parent;
    }

    found = null;
    return false;
}