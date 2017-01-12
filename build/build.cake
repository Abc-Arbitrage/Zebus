#tool nuget:?package=NUnit.Runners.Net4&version=2.6.4
#tool "nuget:?package=GitVersion.CommandLine"
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/Abc.Zebus.Persistence.sln")).FullPath,
    version = MakeAbsolute(File("./../version.txt")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/SharedAssemblyInfo.cs")).FullPath,
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
    var version = System.IO.File.ReadAllText(paths.version);
    var gitVersion = GitVersion();
    version += "-" + gitVersion.Sha;
    Information("Updating AppVeyor build version to " + version);
    AppVeyor.UpdateBuildVersion(version);
});
Task("Clean").Does(() =>
{
    CleanDirectory(paths.output.build);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => NuGetRestore(paths.solution));
Task("Create-AssemblyInfo").Does(()=>{
    var version = System.IO.File.ReadAllText(paths.version);
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
            Product = "Zebus.Persistence",
            Description = "The Persistence service used by Zebus - https://github.com/Abc-Arbitrage/Zebus.Persistence",
            Copyright = "Copyright © ABC arbitrage 2017",
            Company = "ABC arbitrage",
            Version = version,
            FileVersion = version
    });
});
Task("MSBuild").Does(() => MSBuild(paths.solution, settings => settings.SetConfiguration("Release")
                                                                        .SetPlatformTarget(PlatformTarget.MSIL)
                                                                        .WithProperty("OutDir", paths.output.build)));

Task("Run-Unit-Tests").Does(() => NUnit(paths.output.build + "/*.Tests.dll", new NUnitSettings { Framework = "4.6.1", NoResults = true }));
Task("Nuget-Pack").Does(() => 
{
    var version = System.IO.File.ReadAllText(paths.version);
    var settings = new NuGetPackSettings {
        Version = version,
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
    .IsDependentOn("MSBuild");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests");

Task("Nuget")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack")
    .Does(() => {
        var version = System.IO.File.ReadAllText(paths.version);
        Information("   Nuget package is now ready at location: {0}.", paths.output.nuget);
        Warning("   Please remember to create and push a tag based on the currently built version.");
        Information("   You can do so by copying/pasting the following commands:");
        Information("       git tag v{0}", version);
        Information("       git push origin --tags");
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
