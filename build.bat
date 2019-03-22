@echo off
rem We're not really on Azure Pipelines but this disables the Cassandra node requirement
set TF_BUILD=True
powershell -ExecutionPolicy Bypass build\build.ps1 -Script build\build.cake -Target Build
pause
