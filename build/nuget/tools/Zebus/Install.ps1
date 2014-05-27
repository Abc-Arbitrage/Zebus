param($installPath, $toolsPath, $package, $project)

$project.Properties.Item("PreBuildEvent").Value = 'xcopy "'+$toolsPath+'\libzmq-x64-0.0.0.0.dll" "$(TargetDir)" /Y 
xcopy "'+$toolsPath+'\libzmq-x86-0.0.0.0.dll" "$(TargetDir)" /Y'