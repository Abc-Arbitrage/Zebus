#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=NuGet.CommandLine"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    src = MakeAbsolute(Directory("./../src")).FullPath,
    solution = MakeAbsolute(File("./../src/Abc.Zebus.sln")).FullPath,
    props = MakeAbsolute(File("./../src/Directory.Build.props")).FullPath,
    nugetOutput = MakeAbsolute(Directory("./../output/nuget")).FullPath,
};

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean").Does(() =>
{
    CleanDirectories(GetDirectories(paths.src + "/**/bin/Release"));
    CleanDirectory(paths.nugetOutput);
});

Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));

Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings
    .WithTarget("Rebuild")
    .SetConfiguration("Release")
    .SetVerbosity(Verbosity.Minimal)
));

Task("Run-Unit-Tests").Does(() =>
{
    var testProjects = GetFiles("./../src/Abc.Zebus*.Tests/*.csproj");

    foreach (var testProject in testProjects)
    {
        Information($"Testing: {testProject}");

        DotNetCoreTest(testProject.FullPath, new DotNetCoreTestSettings {
            Configuration = "Release",
            NoBuild = true
        });
    }
});

Task("Nuget-Pack").Does(() => MSBuild(paths.solution, settings => settings
        .WithTarget("Pack")
        .SetConfiguration("Release")
        .SetPlatformTarget(PlatformTarget.MSIL)
        .SetVerbosity(Verbosity.Minimal)
        .WithProperty("PackageOutputPath", paths.nugetOutput)
));

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("MSBuild");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

Task("Nuget")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack")
    .Does(() => {
        Information("   Nuget package is now ready at location: {0}.", paths.nugetOutput);
        Warning("   Please remember to create and push a tag based on the currently built version.");
        Information("   You can do so by copying/pasting the following commands:");
        Information("       git tag v{0}", XmlPeek(paths.props, @"/Project/PropertyGroup/PackageVersion/text()"));
        Information("       git push origin --tags");
    });

Task("AppVeyor")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
