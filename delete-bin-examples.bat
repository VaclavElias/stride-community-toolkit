@echo off
REM Deletes the bin and obj folders of the examples and snippets only (everything under examples\);
REM the library, test and tool outputs stay. See delete-bin.bat.

call "%~dp0delete-bin.bat" examples
exit /b %ERRORLEVEL%