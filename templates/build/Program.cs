
using System.Runtime.InteropServices.JavaScript;

string[] paths = [
    "../bff/templates",
        "../identity-server/templates"
];

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

