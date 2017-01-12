#tool nuget:?package=NUnit.Runners.Net4&version=2.6.4
#tool "nuget:?package=GitVersion.CommandLine"
#addin "Cake.Yaml"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/Abc.Zebus.Directory.sln")).FullPath,
    directoryProject = MakeAbsolute(File("./../src/Abc.Zebus.Directory/Abc.Zebus.Directory.csproj")).FullPath,
    version = MakeAbsolute(File("./../version.yml")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/SharedAssemblyInfo.cs")).FullPath,
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

var VersionObject = DeserializeYaml<VersionObjectType>(System.IO.File.ReadAllText(paths.version));

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("UpdateBuildVersionNumber").Does(() =>
{
    if(!AppVeyor.IsRunningOnAppVeyor)
    {
        Information("Not running under AppVeyor");
        return;
    }
    
    Information("Running under AppVeyor");
    var gitVersion = GitVersion();
    var version = VersionObject.version + "-" + gitVersion.Sha;
    Information("Updating AppVeyor build version to " + version);
    AppVeyor.UpdateBuildVersion(version);
});
Task("Clean").Does(() =>
{
    CleanDirectory(paths.output.build);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));
Task("Create-AssemblyInfo").Does(() =>
{
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
            Product = "Zebus.Directory",
            Description = "The Directory service used by Zebus - https://github.com/Abc-Arbitrage/Zebus.Directory",
            Copyright = "Copyright © ABC arbitrage 2017",
            Company = "ABC arbitrage",
            Version = VersionObject.version,
            FileVersion = VersionObject.version,
            InformationalVersion = VersionObject.informational_version
    });
});
Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build)
                                                                        .WithProperty("OverrideOutputType", "Library")));

Task("MSBuild-Standalone").Does(() => MSBuild(paths.directoryProject, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build_standalone)));

Task("Run-Unit-Tests").Does(() =>
{
    NUnit(paths.output.build + "/*.Tests.exe", new NUnitSettings { Framework = "4.6.1", NoResults = true });
    NUnit(paths.output.build + "/*.Tests.dll", new NUnitSettings { Framework = "4.6.1", NoResults = true });
});
Task("Nuget-Pack").Does(() => 
{
    var settings = new NuGetPackSettings {
        Version = VersionObject.informational_version,
        BasePath = paths.output.build,
        OutputDirectory = paths.output.nuget,
        Symbols = true
    };

    NuGetPack(paths.nuspec.directory, settings);
    NuGetPack(paths.nuspec.standalone, new NuGetPackSettings {
        Version = VersionObject.informational_version,
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
    .IsDependentOn("Create-AssemblyInfo")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("MSBuild")
    .IsDependentOn("MSBuild-Standalone");

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
        Information("       git tag v{0}", VersionObject.informational_version);
        Information("       git push origin --tags");
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

private class VersionObjectType
{
    public string version { get; set; }
    public string informational_version { get; set; }
}