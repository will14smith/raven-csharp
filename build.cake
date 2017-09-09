#tool "nuget:?package=NUnit.Runners&version=2.6.4"
#tool "nuget:?package=GitVersion.CommandLine"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

var isAppveyor = BuildSystem.IsRunningOnAppVeyor;

//////////////////////////////////////////////////////////////////////
// VERSION
//////////////////////////////////////////////////////////////////////

var gitVersion = GitVersion(new GitVersionSettings
{
	OutputType          = GitVersionOutput.Json,
	UpdateAssemblyInfo  = false
});

var version = gitVersion.NuGetVersion;

//////////////////////////////////////////////////////////////////////
// CONSTS
//////////////////////////////////////////////////////////////////////

var artifactsDir = Directory("./artifacts");
var outputDir = Directory("./build");

var dotnetFrameworks = new [] { "net45", "net40" };
// net35 can't be build by dotnet - https://github.com/Microsoft/msbuild/issues/1333
var msBuildFrameworks = new [] { "net35" };

var frameworks = dotnetFrameworks.Union(msBuildFrameworks).ToList();

var solution = "src/SharpRaven.sln";
var projects = new [] {
    "src/app/SharpRaven/SharpRaven.csproj",
    "src/app/SharpRaven.Nancy/SharpRaven.Nancy.csproj",

    "src/tests/SharpRaven.Nancy.UnitTests/SharpRaven.Nancy.UnitTests.csproj",
    "src/tests/SharpRaven.UnitTests/SharpRaven.UnitTests.csproj",
};

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (isAppveyor) {
        AppVeyor.UpdateBuildVersion(version);
    }

    Information("Building version {0} of RavenSharp.", version);
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Deletes all files in the artifact and output directories")
    .Does(() =>
	{
		CleanDirectory(artifactsDir);
		CleanDirectory(outputDir);
	});

Task("RestorePackages")
    .Description("Restores packages from nuget using 'dotnet'")
    .Does(() =>
    {
        DotNetCoreRestore(solution);
    });

Task("Build")
    .Description("Builds all versions")
    .IsDependentOn("RestorePackages")
    .Does(() =>
    {
        foreach(var framework in msBuildFrameworks) {
            var settings =  new MSBuildSettings
            {
                Configuration = configuration + "-" + framework,
            };
            settings.WithProperty("OutputPath", new string[] { outputDir + Directory(configuration) + Directory(framework) });
            settings.WithProperty("TargetFramework", new string[] { framework });

            MSBuild(solution, settings);
        }

        foreach(var framework in dotnetFrameworks) {
            DotNetCoreBuild(solution, new DotNetCoreBuildSettings
            {
                Framework = framework,
                Configuration = configuration + "-" + framework,
                OutputDirectory = outputDir + Directory(configuration) + Directory(framework),
            });
        }
    });

Task("Test")
    .Description("Runs all the tests on all the versions")
    .IsDependentOn("Build")
    .Does(() =>
    {
        foreach(var framework in frameworks) {
            var assemblies = GetFiles((outputDir + Directory(configuration) + Directory(framework)).ToString() + "/*.UnitTests.dll");
            if (!assemblies.Any()) {
                throw new FileNotFoundException("Could not find any test assemblies");
            }

            var resultPath = artifactsDir + File(configuration + "-" + framework + "-tests.xml");
            NUnit(assemblies, new NUnitSettings {
                ResultsFile = resultPath,
            });

            if (isAppveyor) {
                AppVeyor.UploadTestResults(resultPath, AppVeyorTestResultsType.NUnit);
            }
        }
    });

//////////////////////////////////////////////////////////////////////
// UPLOAD ARTIFACTS
//////////////////////////////////////////////////////////////////////

Task("UploadArtifacts")
    .Description("Uploads artifacts to AppVeyor")
    // .IsDependentOn("Package")
    .Does(() =>
    {
        foreach(var zip in System.IO.Directory.GetFiles(artifactsDir, "*.nupkg"))
            AppVeyor.UploadArtifact(zip);
    });

//////////////////////////////////////////////////////////////////////
// META TASKS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
    .Description("Rebuilds all versions")
    .IsDependentOn("Clean")
    .IsDependentOn("Build");

// Task("Package")
//     .Description("Packages all versions")
//     .IsDependentOn("PackageFramework");

Task("Appveyor")
    .Description("Builds, tests and packages on AppVeyor")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    // .IsDependentOn("Package")
    .IsDependentOn("UploadArtifacts");

Task("Travis")
    .Description("Builds and tests on Travis")
    .IsDependentOn("Build")
    .IsDependentOn("Test");


Task("Default")
    .Description("Builds all versions")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
