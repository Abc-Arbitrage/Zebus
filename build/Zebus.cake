// Arguments
var target = Argument("target", "Build");

var solution = MakeAbsolute(File("../src/Abc.Zebus.sln"));
var outputDirectory = MakeAbsolute(Directory("../output/build-result"));


Task("Clean")
    .Does(() =>
{
    Information("Cleaning " + outputDirectory);
    CleanDirectory(outputDirectory);
});


Task("Build-Test")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(solution);

    MSBuild(solution, settings => settings.WithProperty("OutputPath", outputDirectory.ToString())
                                          .SetConfiguration("Release")
                                          .SetPlatformTarget(PlatformTarget.MSIL));
});

Task("Test")
    .IsDependentOn("Build-Test")
    .Does(() =>
{
    
    NUnit(outputDirectory + "/*.Tests.dll", new NUnitSettings {
                                                                   ToolPath = MakeAbsolute(File("../tools/nunit/nunit-console.exe")),
                                                                   NoResults = true
                                                              });
});


RunTarget(target);