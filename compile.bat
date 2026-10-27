@echo off
chcp 65001 > nul
echo Compiling D-Day_App.exe ...

set CSC=
for %%v in (4.0.30319) do (
    if exist "C:\Windows\Microsoft.NET\Framework64\v%%v\csc.exe" (
        set "CSC=C:\Windows\Microsoft.NET\Framework64\v%%v\csc.exe"
    )
    if exist "C:\Windows\Microsoft.NET\Framework\v%%v\csc.exe" (
        if not defined CSC set "CSC=C:\Windows\Microsoft.NET\Framework\v%%v\csc.exe"
    )
)

if not defined CSC (
    echo ERROR: csc.exe not found.
    pause & exit /b 1
)

rem Remove old build first so a failed compile is not reported as success
if exist "%~dp0D-Day_App.exe" del "%~dp0D-Day_App.exe"

"%CSC%" /target:winexe /out:"%~dp0D-Day_App.exe" ^
    /reference:System.Windows.Forms.dll ^
    /reference:System.Drawing.dll ^
    /win32icon:"%~dp0icon.ico" ^
    "%~dp0D-Day_App.cs"

if not exist "%~dp0events.txt" (
    if exist "%~dp0events.sample.txt" copy "%~dp0events.sample.txt" "%~dp0events.txt" > nul
)

if exist "%~dp0D-Day_App.exe" (
    echo.
    echo [SUCCESS] D-Day_App.exe created!
    echo Now run D-Day_App.exe directly.
) else (
    echo [FAILED] Compilation failed.
)
pause
