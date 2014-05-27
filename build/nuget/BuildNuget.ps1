$location = Get-Location;
$zebusLocation = [System.IO.Path]::Combine($location, ".\src\Abc.Zebus");
$outputLocation = [System.IO.Path]::Combine($location, "output\nuget");
if( (Test-Path $outputLocation) -eq $false)
{
    $dir = New-Item -ItemType directory $outputLocation;
}

# Get metadata
$file = Get-Item ([System.IO.Path]::Combine($zebusLocation,"bin\Release\Abc.Zebus.dll"));
$assembly = [System.Reflection.Assembly]::LoadFile($file.FullName);
$attributes = $assembly.GetCustomAttributesData();

$description = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyDescriptionAttribute" })[0].ConstructorArguments;
$product = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyProductAttribute" })[0].ConstructorArguments;
$company = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyCompanyAttribute" })[0].ConstructorArguments;
$copyright = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyCopyrightAttribute" })[0].ConstructorArguments;
$configuration = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyConfigurationAttribute" })[0].ConstructorArguments;
$version = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyInformationalVersionAttribute" })[0].ConstructorArguments;

$properties = "`"version="+$version.Value+";title="+$title.Value+";author="+$company.Value+";description="+$description.Value+";copyright="+$copyright.Value+";configuration="+$configuration.Value+"`"";

# Build nuget for Zebus
Write-Host "############# Building Zebus package #############" -ForegroundColor Green
& '.\tools\nuget\NuGet.exe' pack .\build\nuget\nuspecs\Abc.Zebus.nuspec -BasePath $location -OutputDirectory $outputLocation -Properties $properties;

# Build nuget for Zebus.Testing
Write-Host
Write-Host "############# Building Zebus.Testing package #############" -ForegroundColor Green
& '.\tools\nuget\NuGet.exe' pack .\build\nuget\nuspecs\Abc.Zebus.Testing.nuspec -BasePath $location -OutputDirectory $outputLocation -Properties $properties;

# Build nuget for Zebus.Directory
Write-Host
Write-Host "############# Building Zebus.Directory package #############" -ForegroundColor Green
& '.\tools\nuget\NuGet.exe' pack .\build\nuget\nuspecs\Abc.Zebus.Directory.nuspec -BasePath $location -OutputDirectory $outputLocation -Properties $properties -NoPackageAnalysis;