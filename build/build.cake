#l "scripts/utilities.cake"
#tool nuget:?package=NUnit.Runners.Net4&version=2.6.4

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/Abc.Zebus.Persistence.sln")).FullPath,
    version = MakeAbsolute(File("./../version.yml")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/SharedVersionInfo.cs")).FullPath,
    output = new {
        build = MakeAbsolute(Directory("./../output/build")).FullPath,
        nuget = MakeAbsolute(Directory("./../output/nuget")).FullPath,
    },
    nuspec = new {
        persistence = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Persistence.nuspec")).FullPath,
        persistenceCql = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Persistence.CQL.nuspec")).FullPath,
        persistenceCqlTesting = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Persistence.CQL.Testing.nuspec")).FullPath,
    }
};

ReadContext(paths.version);

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("UpdateBuildVersionNumber").Does(() => UpdateAppVeyorBuildVersionNumber());
Task("Clean").Does(() =>
{
    CleanDirectory(paths.output.build);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));
Task("Create-AssemblyInfo").Does(()=>{
    Information("Assembly Version: {0}", VersionContext.AssemblyVersion);
    Information("   NuGet Version: {0}", VersionContext.NugetVersion);
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
        Version = VersionContext.AssemblyVersion,
        FileVersion = VersionContext.AssemblyVersion,
        InformationalVersion = VersionContext.NugetVersion + " Commit: " + VersionContext.Git.Sha
    });
});
Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build)));
Task("Clean-AssemblyInfo").Does(() => System.IO.File.WriteAllText(paths.assemblyInfo, string.Empty));
Task("Run-Unit-Tests").Does(() => NUnit(paths.output.build + "/*.Tests.dll", new NUnitSettings { Framework = "4.6.1", NoResults = true }));
Task("Nuget-Pack").Does(() => 
{
    var settings = new NuGetPackSettings {
        Version = VersionContext.NugetVersion,
        BasePath = paths.output.build,
        OutputDirectory = paths.output.nuget,
        Symbols = true
    };
    NuGetPack(paths.nuspec.persistence, settings);
    NuGetPack(paths.nuspec.persistenceCql, settings);
    NuGetPack(paths.nuspec.persistenceCqlTesting, settings);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("UpdateBuildVersionNumber")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Create-AssemblyInfo")
    .IsDependentOn("MSBuild")
    .IsDependentOn("Clean-AssemblyInfo");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

Task("Nuget")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack")
    .Does(() => {
        Information("   Nuget package is now ready at location: {0}.", paths.output.nuget);
        Warning("   Please remember to create and push a tag based on the currently built version.");
        Information("   You can do so by copying/pasting the following commands:");
        Information("       git tag v{0}", VersionContext.NugetVersion);
        Information("       git push origin --tags");
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
