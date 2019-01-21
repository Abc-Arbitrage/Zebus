
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

Task("NuGet-Pack").Does(() => MSBuild(paths.solution, settings => settings
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
    .IsDependentOn("MSBuild")
    .IsDependentOn("NuGet-Pack");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

Task("AppVeyor")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
