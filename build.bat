@echo off
rem We're not really on AppVeyor but this disables the Cassandra node requirement
set APPVEYOR=True
powershell -ExecutionPolicy Bypass build\build.ps1 -Script build\build.cake -Target Build
pause
