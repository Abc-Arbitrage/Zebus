@echo off
rem We're not really on GitHub Actions but this disables the Cassandra node requirement
set GITHUB_ACTIONS=true
powershell -ExecutionPolicy Bypass build\build.ps1 -Script build\build.cake -Target Test
pause
