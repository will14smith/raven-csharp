#tool "nuget:?package=NUnit.Runners&version=2.6.4"
#tool "nuget:?package=GitVersion.CommandLine"

var target = Argument("target", "Default");
var configurations = Argument("configurations", "Release 3.5,Release 4.0,Release 4.5").Split(',');

var artifactsDir = Directory("./artifacts");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./src/SharpRaven.sln");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
	  foreach(var configuration in configurations) {
        MSBuild("./src/SharpRaven.sln", settings =>
          settings.SetConfiguration(configuration));
	  }
    }
    else
    {
      foreach(var configuration in configurations) {
        XBuild("./src/SharpRaven.sln", settings =>
          settings.SetConfiguration(configuration));
      }
    }
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    var testFiles = GetFiles("./src/tests/**/Release/**/*.UnitTests.dll");
    if (!testFiles.Any())
        throw new FileNotFoundException("Could not find any tests");

    NUnit(testFiles, new NUnitSettings
    {
        ResultsFile = "./artifacts/TestResults.xml",
        Exclude = IsRunningOnWindows() ? null : "NoMono",
    });
});

Task("NuGet-Pack")
	// .IsDependentOn("Test")
	.Does(() => 
{
	var gitVersion = GitVersion(new GitVersionSettings
    {
        OutputType          = GitVersionOutput.Json,
        UpdateAssemblyInfo  = false
    });
	
	var semver = gitVersion.NuGetVersion;
	
    Information("Version: {0}", semver);
	
	NuGetPack("./src/app/SharpRaven/SharpRaven.nuspec", new NuGetPackSettings {
	  Version         = semver,
	  OutputDirectory = "./artifacts/"
	});
	NuGetPack("./src/app/SharpRaven.Nancy/SharpRaven.Nancy.nuspec", new NuGetPackSettings {
	  Version         = semver,
	  OutputDirectory = "./artifacts/"
	});
});

Task("Default")
    .IsDependentOn("Test");

RunTarget(target);
