@echo off
REM Builds the Stride Community Toolkit NuGet packages into the local dev feed at bin\packages.
REM
REM   pack-local.bat                                  packs as 1.0.0-dev (Release)
REM   pack-local.bat --version 1.0.0-dev2             packs a different version
REM   pack-local.bat --configuration Debug            packs a Debug build
REM   pack-local.bat --clean                          removes the feed and cache entries only
REM
REM Any arguments are forwarded to build\pack-local.cs.
REM
REM %~dp0 is this script's own folder, so the .bat works from any working directory. The --file
REM switch is required rather than optional: the repository root contains a .ndproj file, and
REM without --file the SDK would run that project and pass the script to it as an argument.

setlocal

dotnet run --file "%~dp0pack-local.cs" -- %*

exit /b %ERRORLEVEL%
