#l "scripts/utilities.cake"
#tool nuget:?package=NUnit.Runners.Net4&version=2.6.4
#addin "Cake.FileHelpers"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/Abc.Zebus.Directory.sln")).FullPath,
    directoryProject = MakeAbsolute(File("./../src/Abc.Zebus.Directory/Abc.Zebus.Directory.csproj")).FullPath,    version = MakeAbsolute(File("./../version.yml")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/SharedVersionInfo.cs")).FullPath,
    output = new {
        build = MakeAbsolute(Directory("./../output/build/standard")).FullPath,
        build_standalone = MakeAbsolute(Directory("./../output/build/standalone")).FullPath,
        nuget = MakeAbsolute(Directory("./../output/nuget")).FullPath,
    },
    nuspec = new {
        directory = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Directory.nuspec")).FullPath,
        standalone = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Directory.Standalone.nuspec")).FullPath,
        cassandra = MakeAbsolute(File("./nuget/nuspecs/Abc.Zebus.Directory.Cassandra.nuspec")).FullPath,
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
    CleanDirectory(paths.output.build_standalone);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));
Task("Create-AssemblyInfo").Does(()=>{
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
        Version = VersionContext.AssemblyVersion,
        FileVersion = VersionContext.AssemblyVersion,
        InformationalVersion = VersionContext.NugetVersion + " Commit: " + VersionContext.Git.Sha
    });
});
Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build)
                                                                        .WithProperty("OverrideOutputType", "Library")));
Task("MSBuild-Standalone").Does(() => MSBuild(paths.directoryProject, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build_standalone)));
Task("Clean-AssemblyInfo").Does(() => FileWriteText(paths.assemblyInfo, string.Empty));
Task("Run-Unit-Tests").Does(() =>
{
    NUnit(paths.output.build + "/*.Tests.exe", new NUnitSettings { Framework = "4.6.1", NoResults = true });
    NUnit(paths.output.build + "/*.Tests.dll", new NUnitSettings { Framework = "4.6.1", NoResults = true });
});
Task("Nuget-Pack").Does(() => 
{
    var settings = new NuGetPackSettings {
        Version = VersionContext.NugetVersion,
        BasePath = paths.output.build,
        OutputDirectory = paths.output.nuget,
        Symbols = true
    };

    NuGetPack(paths.nuspec.directory, settings);
    NuGetPack(paths.nuspec.standalone, new NuGetPackSettings {
        Version = VersionContext.NugetVersion,
        BasePath = paths.output.build_standalone,
        OutputDirectory = paths.output.nuget,
        Symbols = true
    });
    NuGetPack(paths.nuspec.cassandra, settings);
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
    .IsDependentOn("MSBuild-Standalone")
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