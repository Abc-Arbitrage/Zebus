
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    src = MakeAbsolute(Directory("./../src")).FullPath,
    solution = MakeAbsolute(File("./../src/Abc.Zebus.sln")).FullPath,
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

Task("Compile").Does(() => DotNetCoreMSBuild(paths.solution, new DotNetCoreMSBuildSettings()
    .WithTarget("Restore")
    .WithTarget("Rebuild")
    .WithTarget("Pack")
    .SetConfiguration("Release")
    .WithProperty("PackageOutputPath", paths.nugetOutput)
    .SetMaxCpuCount(0)
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

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Compile");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
