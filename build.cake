#addin Cake.Git

var target = Argument("target", "Default");
var artifactsDir = "./artifacts/";
var solutionPath = "./CodingMilitia.GrpcExtensions.sln";
var project = "./src/CodingMilitia.GrpcExtensions.Hosting/CodingMilitia.GrpcExtensions.Hosting.csproj";
var testProject = "./tests/CodingMilitia.GrpcExtensions.Tests/CodingMilitia.GrpcExtensions.Tests.csproj";
var currentBranch = Argument<string>("currentBranch", GitBranchCurrent("./").FriendlyName);
var isReleaseBuild = string.Equals(currentBranch, "master", StringComparison.OrdinalIgnoreCase);
var configuration = "Release";
var nugetApiKey = Argument<string>("nugetApiKey", null);

Task("Clean")
    .Does(() => {
        if (DirectoryExists(artifactsDir))
        {
            DeleteDirectory(
                artifactsDir, 
                new DeleteDirectorySettings {
                    Recursive = true,
                    Force = true
                }
            );
        }
        CreateDirectory(artifactsDir);
    });

Task("Restore")
    .Does(() => {
        DotNetCoreRestore(solutionPath);
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild(
            solutionPath,
            new DotNetCoreBuildSettings 
            {
                Configuration = configuration
            }
        );
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        DotNetCoreTest(testProject);
    });

Task("Package")
    .IsDependentOn("Test")
    .Does(() => {
        PackageProject("CodingMilitia.GrpcExtensions.Hosting", project, artifactsDir);
    });

Task("Publish")
    .IsDependentOn("Package")
    .Does(() => {
        var pushSettings = new DotNetCoreNuGetPushSettings 
        {
            Source = "https://api.nuget.org/v3/index.json",
            ApiKey = nugetApiKey
        };

        var pkgs = GetFiles(artifactsDir + "*.nupkg");
        foreach(var pkg in pkgs) 
        {
            DotNetCoreNuGetPush(pkg.FullPath, pushSettings);
        }
    });

if(isReleaseBuild)
{
    Information("Release build");
    Task("Default")
        .IsDependentOn("Publish");
}
else
{
    Information("Development build");
    Task("Default")
        .IsDependentOn("Test");
}

RunTarget(target);

private void PackageProject(string projectName, string projectPath, string outputDirectory)
{
    var settings = new DotNetCorePackSettings
        {
            OutputDirectory = outputDirectory,
            NoBuild = true
        };

    DotNetCorePack(projectPath, settings);
}    