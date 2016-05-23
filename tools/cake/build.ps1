Param(
    [string]$Script = "build.cake",
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
	[string]$BuildVersion,
    [switch]$WhatIf
)

$TOOLS_DIR = Join-Path $PSScriptRoot "tools"
$NUGET_EXE = Join-Path $TOOLS_DIR "nuget.exe"
$CAKE_EXE = Join-Path $TOOLS_DIR "Cake/Cake.exe"

# Try download NuGet.exe if do not exist.
if (!(Test-Path $NUGET_EXE)) {
    Write-Host "Downloading nuget.exe"
    Invoke-WebRequest -Uri https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $NUGET_EXE
}

# Make sure NuGet exists where we expect it.
if (!(Test-Path $NUGET_EXE)) {
    Throw "Could not find NuGet.exe"
}

# Restore tools from NuGet.
Push-Location
Set-Location $TOOLS_DIR
Invoke-Expression "$NUGET_EXE install -ExcludeVersion -OutputDirectory $TOOLS_DIR"
Pop-Location
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Make sure that Cake has been installed.
if (!(Test-Path $CAKE_EXE)) {
    Throw "Could not find Cake.exe"
}

# Start Cake
Try
{
	[Environment]::SetEnvironmentVariable("NUGET_EXE", $NUGET_EXE)
	Invoke-Expression "$CAKE_EXE `"$Script`" -target=`"$Target`" -configuration=`"$Configuration`" -verbosity=`"$Verbosity`" -build-version=`"$BuildVersion`" "
}
Finally
{
	[Environment]::SetEnvironmentVariable("NUGET_EXE", $null)
}
Write-Host
exit $LASTEXITCODE