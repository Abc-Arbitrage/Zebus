param($installPath, $toolsPath, $package, $project)

$project.Properties.Item("PreBuildEvent").Value = 'xcopy "C:\Dev\dotnet\lib\ZeroMq\libzmq-x64-0.0.0.0.dll" "$(TargetDir)" /Y 
xcopy "C:\Dev\dotnet\lib\ZeroMq\libzmq-x86-0.0.0.0.dll" "$(TargetDir)" /Y'