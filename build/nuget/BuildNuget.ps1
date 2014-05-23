$location = Get-Location;
$zebusLocation = [System.IO.Path]::Combine($location, "./src/Abc.Zebus");
$zebusTestingLocation = [System.IO.Path]::Combine($location, "./src/Abc.Zebus.Testing");
$outputLocation = [System.IO.Path]::Combine($location, "output/nuget");
if( (Test-Path $outputLocation) -eq $false)
{
    $dir = New-Item -ItemType directory $outputLocation;
}

# Build nuget for Zebus
Write-Host "############# Building Zebus package #############" -ForegroundColor Green
Set-Location $zebusLocation;
cmd /c "C:\Program Files (x86)\NuGet\NuGet.exe" pack -OutputDirectory $outputLocation

Write-Host
Write-Host "############# Building Zebus.Testing package #############" -ForegroundColor Green
# Build nuget for Zebus.Testing
Set-Location $zebusTestingLocation;
cmd /c "C:\Program Files (x86)\NuGet\NuGet.exe" pack -IncludeReferencedProjects -OutputDirectory $outputLocation

Set-Location $location;