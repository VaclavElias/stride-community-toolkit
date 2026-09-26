@echo off
REM Deletes every bin and obj folder under the repository, or under the folders named as arguments.
REM
REM   delete-bin.bat                    the whole repository: src, examples, tests, tools, benchmarks
REM   delete-bin.bat examples tests     only those folders (relative to the repository root)
REM
REM Each folder removed is printed. A folder a running process holds open - Visual Studio with a
REM project loaded, a game still running - is reported as LOCKED and skipped; close it and run again.
REM Nothing git tracks is named bin or obj, so this never touches source. It is not "git clean -xdf",
REM which would also delete every other ignored file, notes and local settings included.
REM
REM %~dp0 is this script's own folder, so the .bat works from any working directory.

setlocal
pushd "%~dp0"

if "%~1"=="" (
    call :sweep "."
) else (
    for %%a in (%*) do call :sweep "%%~a"
)

popd
exit /b 0

:sweep
set /a removed=0
set /a locked=0
for /d /r "%~1" %%d in (bin obj) do (
    if exist "%%d\" (
        rd /s /q "%%d" 2>nul
        if exist "%%d\" (
            echo LOCKED   %%d
            set /a locked+=1
        ) else (
            echo removed  %%d
            set /a removed+=1
        )
    )
)
echo %removed% folders removed under %~1, %locked% locked
exit /b 0