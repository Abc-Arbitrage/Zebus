@echo off
echo Publishing Nugets to Nuget.org / SymbolSource (requires to have the Api key registered)
call tools\nuget\nuget.exe push output\nuget\Zebus.1.0.4.nupkg
call tools\nuget\nuget.exe push output\nuget\Zebus.Directory.1.0.4.nupkg
call tools\nuget\nuget.exe push output\nuget\Zebus.Directory.Standalone.1.0.4.nupkg
call tools\nuget\nuget.exe push output\nuget\Zebus.Testing.1.0.4.nupkg
echo Done
pause