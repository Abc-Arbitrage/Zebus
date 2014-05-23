param($installPath, $toolsPath, $package, $project)

$project.Properties.Item("PreBuildEvent").Value = ""