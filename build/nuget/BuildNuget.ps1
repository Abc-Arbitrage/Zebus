$location = Get-Location;
$zebusLocation = [System.IO.Path]::Combine($location, ".\src\Abc.Zebus");
$outputLocation = [System.IO.Path]::Combine($location, "output\nuget");
$msbuild = 'C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe';
# clean output
if((Test-Path $outputLocation) -eq $true)
{
   Remove-Item -Recurse -Force $outputLocation;
}
$dir = New-Item -ItemType directory $outputLocation;

# Compile solution in release
& '.\tools\nuget\NuGet.exe' restore .\src\Abc.Zebus.sln
& $msbuild .\src\Abc.Zebus.sln /t:rebuild /p:Configuration=Release

# Get metadata (without locking file)
$fileStream = ([System.IO.FileInfo] (Get-Item ([System.IO.Path]::Combine($zebusLocation,"bin\Release\Abc.Zebus.dll")))).OpenRead();
$assemblyBytes = new-object byte[] $fileStream.Length
$fileStream.Read($assemblyBytes, 0, $fileStream.Length);
$fileStream.Close();
$assembly = [System.Reflection.Assembly]::Load($assemblyBytes);
$attributes = $assembly.GetCustomAttributesData();

$description = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyDescriptionAttribute" })[0].ConstructorArguments;
$product = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyProductAttribute" })[0].ConstructorArguments;
$company = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyCompanyAttribute" })[0].ConstructorArguments;
$copyright = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyCopyrightAttribute" })[0].ConstructorArguments;
$configuration = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyConfigurationAttribute" })[0].ConstructorArguments;
$version = ($attributes | ? { $_.AttributeType.Name -eq  "AssemblyInformationalVersionAttribute" })[0].ConstructorArguments;

$properties = "`"version="+$version.Value+";author="+$company.Value+";description="+$description.Value+";copyright="+$copyright.Value+";configuration="+$configuration.Value+"`"";

# Build nuget for Zebus
Write-Host "############# Building Zebus package #############" -ForegroundColor Green
& '.\tools\nuget\NuGet.exe' pack .\build\nuget\nuspecs\Abc.Zebus.nuspec -BasePath $location -OutputDirectory $outputLocation -Properties $properties -sym;

# Build nuget for Zebus.Testing
Write-Host
Write-Host "############# Building Zebus.Testing package #############" -ForegroundColor Green
& '.\tools\nuget\NuGet.exe' pack .\build\nuget\nuspecs\Abc.Zebus.Testing.nuspec -BasePath $location -OutputDirectory $outputLocation -Properties $properties -sym;