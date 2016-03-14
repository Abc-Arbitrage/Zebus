$location = Get-Location;
$outputLocation = [System.IO.Path]::Combine($location, "output\nuget");
if( (Test-Path $outputLocation) -eq $false )
{
    throw "The output directory ($outputLocation) is not found. Please build the packages before trying to publish."
}

foreach($package in [System.IO.Directory]::GetFiles($outputLocation))
{
    $filename = [System.IO.Path]::GetFileName($package);

    Write-Host Pushing $filename -ForegroundColor Green
    & '.\tools\nuget\NuGet.exe' push $package
    Write-Host $filename has been pushed
}